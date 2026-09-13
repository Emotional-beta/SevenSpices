using System.Text.Json;
using System.Text.Json.Serialization;
using SevenSpices.Core.Companions;
using SevenSpices.Core.Content;
using SevenSpices.Core.Game;
using SevenSpices.Core.Ingredients;
using SevenSpices.Core.Items;
using SevenSpices.Core.Run;

namespace SevenSpices.Core.Save;

/// <summary>
/// 存档序列化器（架构文档 §25）：负责「游戏状态 ↔ <see cref="SaveData"/> ↔ JSON 字符串」。
/// <para>
/// 纯 C#，不依赖 Godot；文件读写由表现层负责。缺失 / 非法的存档内容一律显式抛出异常，
/// 不静默丢失玩家持有的内容。
/// </para>
/// </summary>
public static class SaveSerializer
{
    /// <summary>当前存档格式版本。</summary>
    public const int CurrentVersion = 1;

    /// <summary>最低可识别的存档格式版本。</summary>
    public const int MinVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>把存档 DTO 序列化为缩进 JSON 字符串。</summary>
    public static string ToJson(SaveData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return JsonSerializer.Serialize(data, JsonOptions);
    }

    /// <summary>
    /// 从 JSON 字符串反序列化为存档 DTO。空串或无法解析时抛出带原因的异常。
    /// </summary>
    public static SaveData FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("Save JSON cannot be empty.", nameof(json));

        try
        {
            return JsonSerializer.Deserialize<SaveData>(json, JsonOptions)
                ?? throw new InvalidDataException("Save JSON deserialized to null.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Failed to parse save JSON: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 把当前游戏状态抓取成存档 DTO。只抓取架构 §25 规定的状态；
    /// 锅内运行时进度（食材 / 候选池 / Config / VerbLink 等）不在范围内。
    /// </summary>
    public static SaveData Capture(GameState state, MetaState meta)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(meta);

        var run = state.Run;

        var data = new SaveData
        {
            Version = CurrentVersion,
            Run = new RunStateDto
            {
                Chapter = run.Chapter,
                PotIndex = run.PotIndex,
                IsFinalPot = run.IsFinalPot,
                RouteId = run.RouteId,
                RouteActiveChapter = run.RouteActiveChapter,
                RouteTargetsFinalPot = run.RouteTargetsFinalPot,
                ProfessionId = run.ProfessionId,
                IsFailed = run.IsFailed,
                FailReason = run.FailReason,
                Outcome = run.Outcome.ToString(),
                ChapterBossRecords = run.ChapterBossRecords
                    .Select(r => new BossRecordDto
                    {
                        Chapter = r.Chapter,
                        PotIndex = r.PotIndex,
                        BossId = r.BossId,
                        BossName = r.BossName,
                        Satisfied = r.Satisfied,
                        PotTotalFinalScore = r.PotTotalFinalScore,
                        Threshold = r.Threshold,
                        IsFinalPot = r.IsFinalPot,
                    })
                    .ToList(),
            },
            Player = new PlayerStateDto
            {
                Gold = state.Player.Gold,
                IngredientBasket = state.Player.IngredientBasket.Select(ToDto).ToList(),
                Items = state.Player.Items.Select(ToDto).ToList(),
                Companions = state.Player.Companions.Select(ToDto).ToList(),
            },
            Meta = new MetaStateDto
            {
                ImmortalPowders = meta.ImmortalPowders.Select(ToDto).ToList(),
            },
        };

        foreach (var (flavor, value) in state.Bottom.Flavors)
            data.Bottom.Flavors[flavor.ToString()] = value;

        return data;
    }

    /// <summary>
    /// 把存档 DTO 写回目标状态。采用<b>先解析校验、后提交</b>：全部 Definition 查找与枚举解析
    /// 先在临时结构里完成，只有全部成功后才清空并填充目标状态；任何失败都抛
    /// <see cref="InvalidDataException"/> 且<b>不修改任何现有状态</b>（避免「读档失败却丢了当前进度」）。
    /// 提交后对同一状态反复 Apply 幂等。
    /// </summary>
    public static void Apply(SaveData data, GameState state, MetaState meta)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(meta);

        // ── 1. 结构与版本校验（失败不触碰任何现有状态） ─────────────────────────
        ValidateVersion(data.Version);

        var runDto = Require(data.Run, nameof(SaveData.Run));
        var playerDto = Require(data.Player, nameof(SaveData.Player));
        var bottomDto = Require(data.Bottom, nameof(SaveData.Bottom));
        var metaDto = Require(data.Meta, nameof(SaveData.Meta));

        var bossRecordsDto = Require(runDto.ChapterBossRecords, "Run.ChapterBossRecords");
        var basketDto = Require(playerDto.IngredientBasket, "Player.IngredientBasket");
        var itemsDto = Require(playerDto.Items, "Player.Items");
        var companionsDto = Require(playerDto.Companions, "Player.Companions");
        var flavorsDto = Require(bottomDto.Flavors, "Bottom.Flavors");
        var powdersDto = Require(metaDto.ImmortalPowders, "Meta.ImmortalPowders");

        // ── 2. 全部解析到临时结构 ───────────────────────────────────────────────
        RunOutcome outcome;
        List<ChapterBossRecord> bossRecords;
        List<IngredientInstance> basket;
        List<ItemInstance> items;
        List<CompanionInstance> companions;
        List<(FlavorType Flavor, int Value)> flavors;
        List<ItemInstance> powders;

        try
        {
            outcome = ParseOutcome(runDto.Outcome);
            ValidateProfessionId(runDto.ProfessionId);
            ValidateRouteId(runDto.RouteId);
            ValidateRunProgress(runDto);

            bossRecords = new List<ChapterBossRecord>(bossRecordsDto.Count);
            foreach (var dto in bossRecordsDto)
            {
                var record = Require(dto, "Run.ChapterBossRecords[]");
                ValidateBossRecord(record);
                bossRecords.Add(new ChapterBossRecord(
                    record.Chapter, record.PotIndex, record.BossId, record.BossName,
                    record.Satisfied, record.PotTotalFinalScore, record.Threshold, record.IsFinalPot));
            }

            basket = new List<IngredientInstance>(basketDto.Count);
            foreach (var dto in basketDto)
                basket.Add(ToInstance(dto, ResolveIngredientDefinition));

            items = new List<ItemInstance>(itemsDto.Count);
            foreach (var dto in itemsDto)
                items.Add(ToInstance(dto, ResolveItemDefinition));

            companions = new List<CompanionInstance>(companionsDto.Count);
            foreach (var dto in companionsDto)
                companions.Add(ToInstance(dto, ResolveCompanionDefinition));

            if (playerDto.Gold < 0)
                throw new InvalidDataException($"Save Player.Gold {playerDto.Gold} cannot be negative.");

            flavors = new List<(FlavorType Flavor, int Value)>(flavorsDto.Count);
            foreach (var (key, value) in flavorsDto)
            {
                if (value < 0)
                    throw new InvalidDataException(
                        $"Save Bottom.Flavors['{key}'] {value} cannot be negative.");
                flavors.Add((ParseFlavor(key), value));
            }

            powders = new List<ItemInstance>(powdersDto.Count);
            foreach (var dto in powdersDto)
                powders.Add(ToInstance(dto, ResolveItemDefinition));

            if (powders.Count > MetaState.MaxImmortalPowder)
                throw new InvalidDataException(
                    $"Save contains more than {MetaState.MaxImmortalPowder} immortal powders " +
                    $"(found {powders.Count}).");
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // 领域对象构造等抛出的其他异常统一收敛为 InvalidDataException，保持契约一致。
            throw new InvalidDataException($"Save is corrupted: {ex.Message}", ex);
        }

        // ── 3. 全部成功后才提交：清空目标状态并填充 ─────────────────────────────
        var run = state.Run;

        run.ChapterBossRecords.Clear();
        state.Player.IngredientBasket.Clear();
        state.Player.Items.Clear();
        state.Player.Companions.Clear();
        state.Bottom.Clear();
        while (meta.ConsumeImmortalPowder() != null)
        {
        }

        run.Chapter = runDto.Chapter;
        run.PotIndex = runDto.PotIndex;
        run.IsFinalPot = runDto.IsFinalPot;
        // 空 / 空白风潮 Id 视为「无风潮」，与 ProfessionId 的处理一致。
        run.RouteId = string.IsNullOrWhiteSpace(runDto.RouteId) ? null : runDto.RouteId;
        run.RouteActiveChapter = runDto.RouteActiveChapter;
        run.RouteTargetsFinalPot = runDto.RouteTargetsFinalPot;
        // 空 / 空白职业 Id 视为「未指定」（兜底默认职业），避免 Main.OnRestartPressed 传入空串崩溃。
        run.ProfessionId = string.IsNullOrWhiteSpace(runDto.ProfessionId) ? null : runDto.ProfessionId;
        run.IsFailed = runDto.IsFailed;
        run.FailReason = runDto.FailReason;
        run.Outcome = outcome;
        run.ChapterBossRecords.AddRange(bossRecords);

        state.Player.Gold = playerDto.Gold;
        state.Player.IngredientBasket.AddRange(basket);
        state.Player.Items.AddRange(items);
        state.Player.Companions.AddRange(companions);

        foreach (var (flavor, value) in flavors)
            state.Bottom.SetFlavor(flavor, value);

        foreach (var powder in powders)
        {
            if (!meta.TryAddImmortalPowder(powder))
                throw new InvalidDataException(
                    $"Save contains more than {MetaState.MaxImmortalPowder} immortal powders " +
                    $"(offending instance '{powder.InstanceId}').");
        }
    }

    // ── 校验 ──────────────────────────────────────────────────────────────────

    private static T Require<T>(T? value, string fieldName) where T : class =>
        value ?? throw new InvalidDataException($"Save is missing required field '{fieldName}'.");

    private static void ValidateVersion(int version)
    {
        if (version < MinVersion || version > CurrentVersion)
            throw new InvalidDataException(
                $"Unsupported save version {version}; expected between {MinVersion} and {CurrentVersion}.");
    }

    private static void ValidateProfessionId(string? professionId)
    {
        if (string.IsNullOrWhiteSpace(professionId))
            return;

        if (!ProfessionConfig.TryGet(professionId, out _))
            throw new InvalidDataException($"Save references unknown profession '{professionId}'.");
    }

    private static void ValidateRouteId(string? routeId)
    {
        if (string.IsNullOrWhiteSpace(routeId))
            return;

        if (!RouteConfig.TryGet(routeId, out _))
            throw new InvalidDataException($"Save references unknown route '{routeId}'.");
    }

    /// <summary>
    /// 校验本局进度字段的合法范围，尽早失败而不是静默钳值。
    /// 最终锅一致性：<c>IsFinalPot == true</c> 时进度必须为
    /// (ChaptersPerRun, PotsPerChapter)——推进最终锅时章节不再递增。
    /// </summary>
    private static void ValidateRunProgress(RunStateDto runDto)
    {
        if (runDto.Chapter < 1 || runDto.Chapter > RunController.ChaptersPerRun)
            throw new InvalidDataException(
                $"Save Run.Chapter {runDto.Chapter} is out of range [1, {RunController.ChaptersPerRun}].");

        if (runDto.PotIndex < 1 || runDto.PotIndex > RunController.PotsPerChapter)
            throw new InvalidDataException(
                $"Save Run.PotIndex {runDto.PotIndex} is out of range [1, {RunController.PotsPerChapter}].");

        if (runDto.IsFinalPot
            && (runDto.Chapter != RunController.ChaptersPerRun
                || runDto.PotIndex != RunController.PotsPerChapter))
        {
            throw new InvalidDataException(
                $"Save marks Final Pot but progress is ({runDto.Chapter},{runDto.PotIndex}); " +
                $"Final Pot must be ({RunController.ChaptersPerRun},{RunController.PotsPerChapter}).");
        }

        // 风潮字段间一致性（对照 GameController.ApplyRoute 与到期清除逻辑）：
        //  - 无风潮：RouteActiveChapter 必须为 0，且不得标记目标为最终锅；
        //  - 有风潮：RouteActiveChapter ∈ [1, ChaptersPerRun]；生效章节不得早于当前章（否则应已到期清除）；
        //    标记目标为最终锅时，当前章与生效章节都必须等于最后一章
        //    （只有第 3 章末选的风潮才可能目标为最终锅）。
        if (string.IsNullOrWhiteSpace(runDto.RouteId))
        {
            if (runDto.RouteActiveChapter != 0)
                throw new InvalidDataException(
                    $"Save Run.RouteActiveChapter {runDto.RouteActiveChapter} must be 0 when Run.RouteId is empty.");

            if (runDto.RouteTargetsFinalPot)
                throw new InvalidDataException(
                    "Save Run.RouteTargetsFinalPot cannot be true when Run.RouteId is empty.");

            return;
        }

        if (runDto.RouteActiveChapter < 1 || runDto.RouteActiveChapter > RunController.ChaptersPerRun)
        {
            throw new InvalidDataException(
                $"Save Run.RouteActiveChapter {runDto.RouteActiveChapter} is out of range; " +
                $"expected [1, {RunController.ChaptersPerRun}] when Run.RouteId is set.");
        }

        if (runDto.RouteActiveChapter < runDto.Chapter)
        {
            throw new InvalidDataException(
                $"Save Run.RouteActiveChapter {runDto.RouteActiveChapter} is earlier than Run.Chapter " +
                $"{runDto.Chapter}; an expired route should have been cleared.");
        }

        if (runDto.RouteTargetsFinalPot && runDto.RouteActiveChapter != RunController.ChaptersPerRun)
        {
            throw new InvalidDataException(
                $"Save marks RouteTargetsFinalPot but Run.RouteActiveChapter is " +
                $"{runDto.RouteActiveChapter}; expected {RunController.ChaptersPerRun}.");
        }

        if (runDto.RouteTargetsFinalPot && runDto.Chapter != RunController.ChaptersPerRun)
        {
            throw new InvalidDataException(
                $"Save marks RouteTargetsFinalPot but Run.Chapter is {runDto.Chapter}; " +
                $"expected {RunController.ChaptersPerRun}.");
        }
    }

    private static void ValidateBossRecord(BossRecordDto record)
    {
        if (record.Chapter < 1 || record.Chapter > RunController.ChaptersPerRun)
            throw new InvalidDataException(
                $"Save Boss record Chapter {record.Chapter} is out of range [1, {RunController.ChaptersPerRun}].");

        if (record.PotIndex < 1 || record.PotIndex > RunController.PotsPerChapter)
            throw new InvalidDataException(
                $"Save Boss record PotIndex {record.PotIndex} is out of range [1, {RunController.PotsPerChapter}].");

        if (record.Threshold < 0)
            throw new InvalidDataException(
                $"Save Boss record Threshold {record.Threshold} cannot be negative.");

        if (record.PotTotalFinalScore < 0)
            throw new InvalidDataException(
                $"Save Boss record PotTotalFinalScore {record.PotTotalFinalScore} cannot be negative.");
    }

    // ── DTO ↔ 领域对象 ─────────────────────────────────────────────────────────

    private static InstanceDto ToDto(IngredientInstance instance) =>
        new() { InstanceId = instance.InstanceId, DefinitionId = instance.Definition.Id };

    private static InstanceDto ToDto(ItemInstance instance) =>
        new() { InstanceId = instance.InstanceId, DefinitionId = instance.Definition.Id };

    private static InstanceDto ToDto(CompanionInstance instance) =>
        new() { InstanceId = instance.InstanceId, DefinitionId = instance.Definition.Id };

    private static IngredientInstance ToInstance(
        InstanceDto dto, Func<string, IngredientDefinition> resolve) =>
        new(resolve(RequireDefinitionId(dto)), RequireInstanceId(dto));

    private static ItemInstance ToInstance(
        InstanceDto dto, Func<string, ItemDefinition> resolve) =>
        new(resolve(RequireDefinitionId(dto)), RequireInstanceId(dto));

    private static CompanionInstance ToInstance(
        InstanceDto dto, Func<string, CompanionDefinition> resolve) =>
        new(resolve(RequireDefinitionId(dto)), RequireInstanceId(dto));

    private static string RequireDefinitionId(InstanceDto dto)
    {
        if (dto == null)
            throw new InvalidDataException("Save contains a null instance entry.");
        if (string.IsNullOrWhiteSpace(dto.DefinitionId))
            throw new InvalidDataException(
                $"Save instance '{dto.InstanceId}' has an empty definition id.");
        return dto.DefinitionId;
    }

    private static string RequireInstanceId(InstanceDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.InstanceId))
            throw new InvalidDataException(
                $"Save instance of definition '{dto.DefinitionId}' has an empty instance id.");
        return dto.InstanceId;
    }

    // ── Definition 查找（正式 Registry + 专属 Registry；未命中抛清晰异常） ──────────

    private static IngredientDefinition ResolveIngredientDefinition(string id) =>
        IngredientData.Registry.Find(id)
        ?? IngredientData.ProfessionRegistry.Find(id)
        ?? throw new InvalidDataException($"Save references unknown ingredient definition '{id}'.");

    private static ItemDefinition ResolveItemDefinition(string id) =>
        ItemData.Registry.Find(id)
        ?? ItemData.SpecialRegistry.Find(id)
        ?? ItemData.ProfessionRegistry.Find(id)
        ?? throw new InvalidDataException($"Save references unknown item definition '{id}'.");

    private static CompanionDefinition ResolveCompanionDefinition(string id) =>
        CompanionData.Registry.Find(id)
        ?? throw new InvalidDataException($"Save references unknown companion definition '{id}'.");

    // ── 枚举解析 ──────────────────────────────────────────────────────────────

    private static RunOutcome ParseOutcome(string? value)
    {
        if (Enum.TryParse<RunOutcome>(value, ignoreCase: true, out var outcome)
            && Enum.IsDefined(outcome))
        {
            return outcome;
        }

        throw new InvalidDataException($"Save contains unknown run outcome '{value}'.");
    }

    private static FlavorType ParseFlavor(string? key)
    {
        if (Enum.TryParse<FlavorType>(key, ignoreCase: true, out var flavor)
            && Enum.IsDefined(flavor))
        {
            return flavor;
        }

        throw new InvalidDataException($"Save contains unknown flavor '{key}'.");
    }
}
