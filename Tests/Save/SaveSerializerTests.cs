using System.IO;
using SevenSpices.Core.Companions;
using SevenSpices.Core.Content;
using SevenSpices.Core.Game;
using SevenSpices.Core.Ingredients;
using SevenSpices.Core.Items;
using SevenSpices.Core.Pot;
using SevenSpices.Core.Run;
using SevenSpices.Core.Save;
using SevenSpices.Core.Shop;

namespace SevenSpices.Tests.Save;

/// <summary>
/// 存档系统测试（缺口 5）：DTO 往返一致、版本字段、GameController.RestoreSave 集成、
/// 损坏存档显式失败、局外仙丹粉末跨存档往返保留。
/// </summary>
public static class SaveSerializerTests
{
    public static void RunAll()
    {
        Test_RoundTrip_PreservesAllState();
        Test_Version_IsOne();
        Test_Apply_IsIdempotent();
        Test_RestoreSave_Integration_RestartsCurrentPot();
        Test_RestoreSave_AfterFinalSettlement_DoesNotRestartFinalPot();
        Test_RouteTargetsFinalPot_RoundTrip();
        Test_Corrupt_GarbledJson_Throws();
        Test_Corrupt_MissingIngredientDefinition_Throws();
        Test_Corrupt_UnknownFlavor_Throws();
        Test_Corrupt_UnknownOutcome_Throws();
        Test_Corrupt_NullRun_Throws();
        Test_Corrupt_UnknownProfessionId_Throws();
        Test_Corrupt_UnknownRouteId_Throws();
        Test_Corrupt_InvalidVersion_Throws();
        Test_Corrupt_UndefinedEnumValues_Throws();
        Test_Corrupt_ApplyFailure_PreservesExistingState();
        Test_Corrupt_RunChapterOutOfRange_Throws();
        Test_Corrupt_RouteActiveChapterOutOfRange_Throws();
        Test_Corrupt_BossRecordOutOfRange_Throws();
        Test_Corrupt_FinalPotInconsistent_Throws();
        Test_Corrupt_NegativeBottomFlavor_Throws();
        Test_Corrupt_NegativeGold_Throws();
        Test_BlankRouteId_NormalizedToNull();
        Test_MetaPowder_RoundTrip_And_Cleared();

        Console.WriteLine("All SaveSerializerTests passed.");
    }

    // ── 辅助 ─────────────────────────────────────────────────────────────────

    static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception($"[FAIL] SaveSerializerTests: {message}");
    }

    static void AssertThrows<TException>(Action action, string message) where TException : Exception
    {
        bool threw = false;
        try { action(); }
        catch (TException) { threw = true; }
        Assert(threw, message);
    }

    /// <summary>构造含职业专属 / 普通食材、道具、伙伴、仙丹粉末、Boss 记录、风潮、金币、锅底的状态。</summary>
    static (GameState State, MetaState Meta) BuildRichState()
    {
        var state = new GameState();
        var meta = new MetaState();

        var run = state.Run;
        run.Chapter = 2;
        run.PotIndex = 2;
        run.IsFinalPot = false;
        run.RouteId = "trend_sweet";
        run.RouteActiveChapter = 3;
        run.ProfessionId = "sweet";
        run.IsFailed = false;
        run.FailReason = null;
        run.Outcome = RunOutcome.Restart;

        run.ChapterBossRecords.Add(new ChapterBossRecord(
            chapter: 1, potIndex: 3, bossId: "taotie_child", bossName: "小饕餮",
            satisfied: true, potTotalFinalScore: 120, threshold: 100, isFinalPot: false));
        run.ChapterBossRecords.Add(new ChapterBossRecord(
            chapter: 3, potIndex: 3, bossId: "taotie_true", bossName: "真身",
            satisfied: false, potTotalFinalScore: 900, threshold: 1000, isFinalPot: true));

        state.Player.Gold = 77;
        state.Player.IngredientBasket.Add(new IngredientInstance(IngredientData.Rice, "bi-rice-1"));
        state.Player.IngredientBasket.Add(new IngredientInstance(IngredientData.Honey, "bi-honey-1"));
        state.Player.IngredientBasket.Add(new IngredientInstance(IngredientData.PickledBamboo, "bi-bamboo-1"));

        state.Player.Items.Add(new ItemInstance(ItemData.Salt, "it-salt-1"));
        state.Player.Items.Add(new ItemInstance(ItemData.EtchingPrimer, "it-etching-1"));
        state.Player.Items.Add(new ItemInstance(ItemData.ImmortalPowder, "it-powder-1"));

        state.Player.Companions.Add(new CompanionInstance(CompanionData.SweetBossCompanion, "co-sweet-1"));
        state.Player.Companions.Add(new CompanionInstance(CompanionData.GenerousGuestCompanion, "co-guest-1"));

        state.Bottom.SetFlavor(FlavorType.Sweet, 5);
        state.Bottom.SetFlavor(FlavorType.Spicy, 2);

        meta.TryAddImmortalPowder(new ItemInstance(ItemData.ImmortalPowder, "meta-powder-1"));

        return (state, meta);
    }

    static void AssertIngredientsEqual(
        IReadOnlyList<IngredientInstance> expected, IReadOnlyList<IngredientInstance> actual, string label)
    {
        Assert(expected.Count == actual.Count,
            $"{label}: 数量应为 {expected.Count}，实际 {actual.Count}");
        for (int i = 0; i < expected.Count; i++)
        {
            Assert(expected[i].InstanceId == actual[i].InstanceId,
                $"{label}[{i}]: InstanceId 应为 {expected[i].InstanceId}，实际 {actual[i].InstanceId}");
            Assert(expected[i].Definition.Id == actual[i].Definition.Id,
                $"{label}[{i}]: Definition.Id 应为 {expected[i].Definition.Id}，实际 {actual[i].Definition.Id}");
        }
    }

    static void AssertItemsEqual(
        IReadOnlyList<ItemInstance> expected, IReadOnlyList<ItemInstance> actual, string label)
    {
        Assert(expected.Count == actual.Count,
            $"{label}: 数量应为 {expected.Count}，实际 {actual.Count}");
        for (int i = 0; i < expected.Count; i++)
        {
            Assert(expected[i].InstanceId == actual[i].InstanceId,
                $"{label}[{i}]: InstanceId 应为 {expected[i].InstanceId}，实际 {actual[i].InstanceId}");
            Assert(expected[i].Definition.Id == actual[i].Definition.Id,
                $"{label}[{i}]: Definition.Id 应为 {expected[i].Definition.Id}，实际 {actual[i].Definition.Id}");
        }
    }

    // ── 1. 往返一致 ───────────────────────────────────────────────────────────

    static void Test_RoundTrip_PreservesAllState()
    {
        var (state, meta) = BuildRichState();

        string json = SaveSerializer.ToJson(SaveSerializer.Capture(state, meta));
        var parsed = SaveSerializer.FromJson(json);

        var target = new GameState();
        var targetMeta = new MetaState();
        SaveSerializer.Apply(parsed, target, targetMeta);

        var run = target.Run;
        Assert(run.Chapter == 2 && run.PotIndex == 2, $"Run 进度应为 (2,2)，实际 ({run.Chapter},{run.PotIndex})");
        Assert(!run.IsFinalPot, "IsFinalPot 应还原为 false");
        Assert(run.RouteId == "trend_sweet", $"RouteId 应为 trend_sweet，实际 {run.RouteId}");
        Assert(run.RouteActiveChapter == 3, $"RouteActiveChapter 应为 3，实际 {run.RouteActiveChapter}");
        Assert(run.ProfessionId == "sweet", $"ProfessionId 应为 sweet，实际 {run.ProfessionId}");
        Assert(!run.IsFailed, "IsFailed 应还原为 false");
        Assert(run.FailReason == null, "FailReason 应还原为 null");
        Assert(run.Outcome == RunOutcome.Restart, $"Outcome 应还原为 Restart，实际 {run.Outcome}");

        Assert(run.ChapterBossRecords.Count == 2, $"Boss 记录应为 2 条，实际 {run.ChapterBossRecords.Count}");
        var first = run.ChapterBossRecords[0];
        var second = run.ChapterBossRecords[1];
        Assert(first.Chapter == 1 && first.PotIndex == 3 && first.BossId == "taotie_child"
               && first.BossName == "小饕餮" && first.Satisfied && first.PotTotalFinalScore == 120
               && first.Threshold == 100 && !first.IsFinalPot,
            "第一条 Boss 记录字段应完全一致");
        Assert(second.IsFinalPot && second.BossId == "taotie_true" && !second.Satisfied
               && second.PotTotalFinalScore == 900 && second.Threshold == 1000,
            "第二条（最终锅真身）Boss 记录字段应完全一致");

        Assert(target.Player.Gold == 77, $"金币应为 77，实际 {target.Player.Gold}");
        AssertIngredientsEqual(state.Player.IngredientBasket, target.Player.IngredientBasket, "食材篮");
        AssertItemsEqual(state.Player.Items, target.Player.Items, "道具栏");

        Assert(target.Player.Companions.Count == 2, $"伙伴应为 2 位，实际 {target.Player.Companions.Count}");
        Assert(target.Player.Companions[0].InstanceId == "co-sweet-1"
               && target.Player.Companions[0].Definition.Id == "sweet_boss",
            "第一位伙伴实例应保持一致");
        Assert(target.Player.Companions[1].InstanceId == "co-guest-1"
               && target.Player.Companions[1].Definition.Id == "generous_guest",
            "第二位伙伴实例应保持一致");

        Assert(target.Bottom.GetFlavor(FlavorType.Sweet) == 5, "锅底甜味应为 5");
        Assert(target.Bottom.GetFlavor(FlavorType.Spicy) == 2, "锅底辣味应为 2");
        Assert(target.Bottom.Flavors.Count == 2, $"锅底应只含 2 种味道，实际 {target.Bottom.Flavors.Count}");
    }

    // ── 2. 版本字段 ───────────────────────────────────────────────────────────

    static void Test_Version_IsOne()
    {
        var (state, meta) = BuildRichState();
        var data = SaveSerializer.Capture(state, meta);

        Assert(SaveSerializer.CurrentVersion == 1, "CurrentVersion 应为 1");
        Assert(data.Version == 1, $"Capture 的 Version 应为 1，实际 {data.Version}");
        Assert(SaveSerializer.FromJson(SaveSerializer.ToJson(data)).Version == 1,
            "JSON 往返后 Version 应仍为 1");
    }

    // ── 3. Apply 幂等 ─────────────────────────────────────────────────────────

    static void Test_Apply_IsIdempotent()
    {
        var (state, meta) = BuildRichState();
        var data = SaveSerializer.FromJson(SaveSerializer.ToJson(SaveSerializer.Capture(state, meta)));

        var target = new GameState();
        var targetMeta = new MetaState();
        SaveSerializer.Apply(data, target, targetMeta);
        SaveSerializer.Apply(data, target, targetMeta);

        Assert(target.Player.IngredientBasket.Count == 3, "重复 Apply 后食材篮不应增长");
        Assert(target.Player.Items.Count == 3, "重复 Apply 后道具栏不应增长");
        Assert(target.Player.Companions.Count == 2, "重复 Apply 后伙伴不应增长");
        Assert(target.Run.ChapterBossRecords.Count == 2, "重复 Apply 后 Boss 记录不应增长");
        Assert(targetMeta.ImmortalPowderCount == 1, "重复 Apply 后仙丹粉末不应增长");
    }

    // ── 4. RestoreSave 集成：从当前锅开头重新开始 ─────────────────────────────

    /// <summary>用「能选就选、池空就跳」走完当前普通锅，并处理锅结束链（本用例奖励 / 商店已关闭）。</summary>
    static void FinishCurrentPot(GameController gc)
    {
        int guard = 0;
        while (gc.Pot.Phase == PotPhase.InProgress && guard++ < 500)
        {
            if (gc.CanSelectIngredient)
                gc.SelectIngredient(gc.CurrentCandidates[0].InstanceId);
            else if (gc.CanSkipBowl)
                gc.SkipBowl();
            else
                break;
        }

        Assert(gc.Pot.Phase == PotPhase.Ended, "FinishCurrentPot: 普通锅应已 Ended");

        if (gc.IsAwaitingReward)
            gc.ChooseReward(gc.RewardCandidates[0].InstanceId);
        if (gc.IsAwaitingCompanionChoice)
            gc.SkipCompanionChoice();
        if (gc.IsShopOpen)
            gc.SkipShop();

        // 每章第 3 锅商店后开放路线：优先选风潮（写入 RouteId），便于断言风潮随存档恢复。
        if (gc.IsAwaitingRouteChoice)
        {
            var trend = gc.RouteOffers.FirstOrDefault(r => r.Kind == RouteKind.FlavorTrend);
            if (trend != null)
                gc.ChooseRoute(trend.Id);
            else
                gc.SkipRoute();
        }
    }

    static void Test_RestoreSave_Integration_RestartsCurrentPot()
    {
        // 关闭稀有食客 / 奖励 / 商店，聚焦推进与存档恢复本身。
        var appearance = new CustomerAppearanceConfig { RareBowlNumbers = Array.Empty<int>() };
        var potReward = new PotRewardConfig { ChoiceCount = 0 };
        var shop = new ShopConfig { IngredientOfferCount = 0, ItemOfferCount = 0 };

        var state = new GameState();
        var meta = new MetaState();
        var gc = new GameController(state, appearance, new Random(2026), potReward, shop, metaState: meta);
        gc.StartNewGame();

        // 打通第 1 章 3 锅并推进到第 2 章第 1 锅。
        FinishCurrentPot(gc); gc.AdvanceToNextPot();   // (1,2)
        FinishCurrentPot(gc); gc.AdvanceToNextPot();   // (1,3)
        FinishCurrentPot(gc); gc.AdvanceToNextPot();   // (2,1)

        Assert(gc.Run.Chapter == 2 && gc.Run.PotIndex == 1,
            $"前置：应到达 (2,1)，实际 ({gc.Run.Chapter},{gc.Run.PotIndex})");
        Assert(gc.Run.RouteId != null, "前置：第 1 章末应已选风潮并写入 RouteId");
        Assert(gc.Pot.Phase == PotPhase.InProgress, "前置：第 2 章第 1 锅应进行中");

        // 抓取存档 → JSON → 新建控制器（新 GameState + 新 MetaState）恢复。
        string json = SaveSerializer.ToJson(gc.CaptureSave());

        var target = new GameState();
        var targetMeta = new MetaState();
        var gc2 = new GameController(target, appearance, new Random(999), potReward, shop, metaState: targetMeta);
        gc2.RestoreSave(SaveSerializer.FromJson(json));

        Assert(gc2.Run.Chapter == gc.Run.Chapter && gc2.Run.PotIndex == gc.Run.PotIndex,
            $"恢复后进度应一致 ({gc.Run.Chapter},{gc.Run.PotIndex})，实际 ({gc2.Run.Chapter},{gc2.Run.PotIndex})");
        Assert(gc2.Run.ProfessionId == gc.Run.ProfessionId, "恢复后职业应一致");
        Assert(gc2.Run.RouteId == gc.Run.RouteId, "恢复后风潮应一致");
        Assert(gc2.Run.RouteActiveChapter == gc.Run.RouteActiveChapter, "恢复后风潮生效章节应一致");
        Assert(gc2.Player.Gold == gc.Player.Gold, $"恢复后金币应一致（{gc.Player.Gold}）");
        Assert(gc2.Player.IngredientBasket.Count == gc.Player.IngredientBasket.Count,
            "恢复后食材篮数量应一致");
        Assert(gc2.Player.Items.Count == gc.Player.Items.Count, "恢复后道具数量应一致");

        // 读档后从当前锅开头重新开始。
        Assert(gc2.Pot.Phase == PotPhase.InProgress, $"读档后锅应重新开始（InProgress），实际 {gc2.Pot.Phase}");
        Assert(gc2.CanSelectIngredient, "读档后应可重新抽取 / 选择食材");
    }

    // ── 4b. 终局存档恢复：已结算完成的最终锅不得被重新开出来 ───────────────────

    /// <summary>
    /// 最终锅结算（Outcome 落库）后存档再读档：本局应保持完成态、不重开最终锅、Boss 记录不重复。
    /// </summary>
    static void Test_RestoreSave_AfterFinalSettlement_DoesNotRestartFinalPot()
    {
        var state = new GameState();
        var gc = new GameController(state);
        gc.StartNewGame();

        // 直达最终锅并结算。
        gc.Run.Chapter = RunController.ChaptersPerRun;
        gc.Run.PotIndex = RunController.PotsPerChapter;
        gc.Run.IsFinalPot = true;
        gc.StartCurrentPot();
        state.Pot.BaseScore = 1;
        gc.EndCooking();

        Assert(gc.Run.Outcome != RunOutcome.Unsettled,
            "前置：最终锅结算后 Outcome 不应为 Unsettled");
        int bossCount = gc.Run.ChapterBossRecords.Count;
        Assert(bossCount == 1, $"前置：最终锅结算应恰好追加 1 条 Boss 记录，实际 {bossCount}");
        Assert(gc.IsRunComplete, "前置：最终锅结算后 IsRunComplete 应为 true");

        var save = gc.CaptureSave();

        var target = new GameState();
        var gc2 = new GameController(target);
        gc2.RestoreSave(save);

        Assert(gc2.IsRunComplete, "读档后 IsRunComplete 应保持 true");
        Assert(gc2.Run.IsFinalPot, "读档后应保持最终锅标记");
        Assert(gc2.Pot.Phase != PotPhase.InProgress,
            $"读档后最终锅不应被重新开成 InProgress，实际 {gc2.Pot.Phase}");
        Assert(gc2.Run.Outcome == gc.Run.Outcome, "读档后结局应保持一致");
        Assert(gc2.Run.ChapterBossRecords.Count == bossCount,
            $"读档不应重复追加 Boss 记录（期望 {bossCount}，实际 {gc2.Run.ChapterBossRecords.Count}）");
    }

    // ── 4c. 第 3 章末风潮「目标为最终锅」标记往返 ─────────────────────────────

    /// <summary>
    /// 第 3 章末选风潮（目标为最终锅）写入的 RouteTargetsFinalPot 应随存档往返保留；
    /// 该标记只在第 3 章末为真，负样本确认往返不会把它恒真化。
    /// </summary>
    static void Test_RouteTargetsFinalPot_RoundTrip()
    {
        var state = new GameState();
        state.Run.RouteId = "trend_sweet";
        state.Run.RouteActiveChapter = 3;
        state.Run.RouteTargetsFinalPot = true;

        string json = SaveSerializer.ToJson(SaveSerializer.Capture(state, new MetaState()));
        var target = new GameState();
        SaveSerializer.Apply(SaveSerializer.FromJson(json), target, new MetaState());

        Assert(target.Run.RouteId == "trend_sweet",
            $"往返后 RouteId 应为 trend_sweet，实际 {target.Run.RouteId}");
        Assert(target.Run.RouteActiveChapter == 3,
            $"往返后 RouteActiveChapter 应为 3，实际 {target.Run.RouteActiveChapter}");
        Assert(target.Run.RouteTargetsFinalPot, "往返后 RouteTargetsFinalPot 应保留 true");

        // 负样本：false 往返后仍为 false（证明不是恒 true）。
        state.Run.RouteTargetsFinalPot = false;
        json = SaveSerializer.ToJson(SaveSerializer.Capture(state, new MetaState()));
        var target2 = new GameState();
        SaveSerializer.Apply(SaveSerializer.FromJson(json), target2, new MetaState());
        Assert(!target2.Run.RouteTargetsFinalPot, "往返后 RouteTargetsFinalPot 应保留 false");
    }

    // ── 5. 损坏存档显式失败 ───────────────────────────────────────────────────

    static void Test_Corrupt_GarbledJson_Throws()
    {
        AssertThrows<InvalidDataException>(
            () => SaveSerializer.FromJson("{ this is not valid json ]"),
            "乱码 JSON 应抛 InvalidDataException");

        AssertThrows<ArgumentException>(
            () => SaveSerializer.FromJson("   "),
            "空 JSON 应抛 ArgumentException");
    }

    static void Test_Corrupt_MissingIngredientDefinition_Throws()
    {
        var data = new SaveData();
        data.Run.ProfessionId = "sour";
        data.Player.IngredientBasket.Add(new InstanceDto
        {
            InstanceId = "ghost-1",
            DefinitionId = "does_not_exist",
        });

        bool threw = false;
        try
        {
            SaveSerializer.Apply(data, new GameState(), new MetaState());
        }
        catch (InvalidDataException ex)
        {
            threw = true;
            Assert(ex.Message.Contains("does_not_exist"),
                $"异常信息应包含缺失的 Definition id，实际：{ex.Message}");
        }

        Assert(threw, "缺失 Definition 应抛 InvalidDataException（而非静默丢内容）");
    }

    static void Test_Corrupt_UnknownFlavor_Throws()
    {
        var data = new SaveData();
        data.Bottom.Flavors["not_a_flavor"] = 1;

        AssertThrows<InvalidDataException>(
            () => SaveSerializer.Apply(data, new GameState(), new MetaState()),
            "未知味道名应抛 InvalidDataException");
    }

    static void Test_Corrupt_UnknownOutcome_Throws()
    {
        var data = new SaveData();
        data.Run.Outcome = "bogus_outcome";

        AssertThrows<InvalidDataException>(
            () => SaveSerializer.Apply(data, new GameState(), new MetaState()),
            "未知结局枚举应抛 InvalidDataException");
    }

    /// <summary>显式 null 的必需对象（如 {"Run":null}）应抛 InvalidDataException 而非 NRE。</summary>
    static void Test_Corrupt_NullRun_Throws()
    {
        var data = new SaveData { Run = null! };
        AssertThrows<InvalidDataException>(
            () => SaveSerializer.Apply(data, new GameState(), new MetaState()),
            "{\"Run\":null} 应抛 InvalidDataException");

        var playerNull = new SaveData { Player = null! };
        AssertThrows<InvalidDataException>(
            () => SaveSerializer.Apply(playerNull, new GameState(), new MetaState()),
            "{\"Player\":null} 应抛 InvalidDataException");

        var listNull = new SaveData();
        listNull.Player.Items = null!;
        AssertThrows<InvalidDataException>(
            () => SaveSerializer.Apply(listNull, new GameState(), new MetaState()),
            "必需列表为 null 应抛 InvalidDataException");
    }

    static void Test_Corrupt_UnknownProfessionId_Throws()
    {
        var data = new SaveData();
        data.Run.ProfessionId = "ghost_profession";

        AssertThrows<InvalidDataException>(
            () => SaveSerializer.Apply(data, new GameState(), new MetaState()),
            "未知 ProfessionId 应抛 InvalidDataException");
    }

    static void Test_Corrupt_UnknownRouteId_Throws()
    {
        var data = new SaveData();
        data.Run.RouteId = "ghost_route";

        AssertThrows<InvalidDataException>(
            () => SaveSerializer.Apply(data, new GameState(), new MetaState()),
            "未知 RouteId 应抛 InvalidDataException");
    }

    static void Test_Corrupt_InvalidVersion_Throws()
    {
        var tooLow = new SaveData { Version = SaveSerializer.MinVersion - 1 };
        AssertThrows<InvalidDataException>(
            () => SaveSerializer.Apply(tooLow, new GameState(), new MetaState()),
            "Version 低于下界应抛 InvalidDataException");

        var tooHigh = new SaveData { Version = SaveSerializer.CurrentVersion + 1 };
        AssertThrows<InvalidDataException>(
            () => SaveSerializer.Apply(tooHigh, new GameState(), new MetaState()),
            "Version 超过上界应抛 InvalidDataException");
    }

    /// <summary>数字字符串（如 "7"）不会被静默解析为非法枚举值。</summary>
    static void Test_Corrupt_UndefinedEnumValues_Throws()
    {
        var outcome = new SaveData();
        outcome.Run.Outcome = "7";
        AssertThrows<InvalidDataException>(
            () => SaveSerializer.Apply(outcome, new GameState(), new MetaState()),
            "未定义 Outcome 数值 \"7\" 应抛 InvalidDataException");

        var flavor = new SaveData();
        flavor.Bottom.Flavors["7"] = 1;
        AssertThrows<InvalidDataException>(
            () => SaveSerializer.Apply(flavor, new GameState(), new MetaState()),
            "未定义 FlavorType 数值 \"7\" 应抛 InvalidDataException");
    }

    /// <summary>Apply 非事务性修复：解析中途失败时，目标状态的既有进度必须原封不动。</summary>
    static void Test_Corrupt_ApplyFailure_PreservesExistingState()
    {
        var (state, meta) = BuildRichState();
        var valid = SaveSerializer.FromJson(SaveSerializer.ToJson(SaveSerializer.Capture(state, meta)));

        var target = new GameState();
        var targetMeta = new MetaState();
        SaveSerializer.Apply(valid, target, targetMeta);

        // 损坏存档：在清单末尾追加一个未知 Definition，模拟解析中途失败。
        var corrupt = SaveSerializer.FromJson(SaveSerializer.ToJson(SaveSerializer.Capture(state, meta)));
        corrupt.Player.IngredientBasket.Add(new InstanceDto
        {
            InstanceId = "ghost-1",
            DefinitionId = "does_not_exist",
        });

        int basketBefore = target.Player.IngredientBasket.Count;
        int itemsBefore = target.Player.Items.Count;
        int companionsBefore = target.Player.Companions.Count;
        int goldBefore = target.Player.Gold;
        int bossBefore = target.Run.ChapterBossRecords.Count;
        int metaPowderBefore = targetMeta.ImmortalPowderCount;
        string? professionBefore = target.Run.ProfessionId;

        AssertThrows<InvalidDataException>(
            () => SaveSerializer.Apply(corrupt, target, targetMeta),
            "损坏存档应抛 InvalidDataException");

        Assert(target.Player.IngredientBasket.Count == basketBefore, "失败后食材篮不应被清空");
        Assert(target.Player.Items.Count == itemsBefore, "失败后道具栏不应被清空");
        Assert(target.Player.Companions.Count == companionsBefore, "失败后伙伴不应被清空");
        Assert(target.Player.Gold == goldBefore, "失败后金币不应被改写");
        Assert(target.Run.ChapterBossRecords.Count == bossBefore, "失败后 Boss 记录不应被清空");
        Assert(targetMeta.ImmortalPowderCount == metaPowderBefore, "失败后局外粉末不应被清空");
        Assert(target.Run.ProfessionId == professionBefore, "失败后职业不应被改写");
    }

    // ── 5b. 非法值尽早失败（不静默钳值） ───────────────────────────────────────

    static void Test_Corrupt_RunChapterOutOfRange_Throws()
    {
        var low = new SaveData();
        low.Run.Chapter = 0;
        AssertThrows<InvalidDataException>(
            () => SaveSerializer.Apply(low, new GameState(), new MetaState()),
            "Chapter 低于下界应抛 InvalidDataException");

        var high = new SaveData();
        high.Run.Chapter = RunController.ChaptersPerRun + 1;
        AssertThrows<InvalidDataException>(
            () => SaveSerializer.Apply(high, new GameState(), new MetaState()),
            "Chapter 超过上界应抛 InvalidDataException");

        var potLow = new SaveData();
        potLow.Run.PotIndex = 0;
        AssertThrows<InvalidDataException>(
            () => SaveSerializer.Apply(potLow, new GameState(), new MetaState()),
            "PotIndex 低于下界应抛 InvalidDataException");
    }

    /// <summary>RouteActiveChapter 只允许 0 或 [1, ChaptersPerRun]；越界应尽早失败且不改目标状态。</summary>
    static void Test_Corrupt_RouteActiveChapterOutOfRange_Throws()
    {
        var high = new SaveData();
        high.Run.RouteActiveChapter = RunController.ChaptersPerRun + 1;
        AssertThrows<InvalidDataException>(
            () => SaveSerializer.Apply(high, new GameState(), new MetaState()),
            "RouteActiveChapter 超过上界应抛 InvalidDataException");

        var low = new SaveData();
        low.Run.RouteActiveChapter = -1;
        AssertThrows<InvalidDataException>(
            () => SaveSerializer.Apply(low, new GameState(), new MetaState()),
            "RouteActiveChapter 为负应抛 InvalidDataException");

        // 目标状态未被修改：越界解析在校验阶段失败，不应提交任何字段。
        var target = new GameState();
        target.Run.Chapter = 2;
        target.Run.PotIndex = 2;
        target.Run.RouteActiveChapter = 1;
        AssertThrows<InvalidDataException>(
            () => SaveSerializer.Apply(high, target, new MetaState()),
            "RouteActiveChapter 越界应抛 InvalidDataException（目标态版本）");
        Assert(target.Run.Chapter == 2 && target.Run.PotIndex == 2
               && target.Run.RouteActiveChapter == 1,
            "RouteActiveChapter 越界时目标状态不应被修改");
    }

    /// <summary>Boss 记录的 Chapter / PotIndex 越界或 Threshold / PotTotalFinalScore 为负应被拒，且不改目标状态。</summary>
    static void Test_Corrupt_BossRecordOutOfRange_Throws()
    {
        static BossRecordDto Record(int chapter, int potIndex, int threshold, int score) => new()
        {
            Chapter = chapter,
            PotIndex = potIndex,
            BossId = "taotie_child",
            BossName = "小饕餮",
            PotTotalFinalScore = score,
            Threshold = threshold,
        };

        var chapterOut = new SaveData();
        chapterOut.Run.ChapterBossRecords.Add(
            Record(RunController.ChaptersPerRun + 1, 1, threshold: 1, score: 1));
        AssertThrows<InvalidDataException>(
            () => SaveSerializer.Apply(chapterOut, new GameState(), new MetaState()),
            "Boss 记录 Chapter 越界应抛 InvalidDataException");

        var potOut = new SaveData();
        potOut.Run.ChapterBossRecords.Add(Record(1, 0, threshold: 1, score: 1));
        AssertThrows<InvalidDataException>(
            () => SaveSerializer.Apply(potOut, new GameState(), new MetaState()),
            "Boss 记录 PotIndex 越界应抛 InvalidDataException");

        var negativeThreshold = new SaveData();
        negativeThreshold.Run.ChapterBossRecords.Add(Record(1, 1, threshold: -1, score: 1));
        AssertThrows<InvalidDataException>(
            () => SaveSerializer.Apply(negativeThreshold, new GameState(), new MetaState()),
            "Boss 记录 Threshold 为负应抛 InvalidDataException");

        var negativeScore = new SaveData();
        negativeScore.Run.ChapterBossRecords.Add(Record(1, 1, threshold: 1, score: -1));
        AssertThrows<InvalidDataException>(
            () => SaveSerializer.Apply(negativeScore, new GameState(), new MetaState()),
            "Boss 记录 PotTotalFinalScore 为负应抛 InvalidDataException");

        // 目标状态未被修改：既有 Boss 记录应原封不动。
        var target = new GameState();
        target.Run.ChapterBossRecords.Add(new ChapterBossRecord(
            chapter: 1, potIndex: 1, bossId: "taotie_child", bossName: "小饕餮",
            satisfied: true, potTotalFinalScore: 100, threshold: 50, isFinalPot: false));
        AssertThrows<InvalidDataException>(
            () => SaveSerializer.Apply(potOut, target, new MetaState()),
            "Boss 记录越界应抛 InvalidDataException（目标态版本）");
        Assert(target.Run.ChapterBossRecords.Count == 1
               && target.Run.ChapterBossRecords[0].Threshold == 50
               && target.Run.ChapterBossRecords[0].BossId == "taotie_child",
            "Boss 记录越界时目标状态不应被修改");
    }

    static void Test_Corrupt_FinalPotInconsistent_Throws()
    {
        // 默认进度为 (1,1)：标记最终锅但进度不匹配应被拒。
        var data = new SaveData();
        data.Run.IsFinalPot = true;
        AssertThrows<InvalidDataException>(
            () => SaveSerializer.Apply(data, new GameState(), new MetaState()),
            "最终锅标记与 (1,1) 进度不一致应抛 InvalidDataException");

        // 合法最终锅进度 (3,3) 应通过（负样本对照）。
        var ok = new SaveData();
        ok.Run.Chapter = RunController.ChaptersPerRun;
        ok.Run.PotIndex = RunController.PotsPerChapter;
        ok.Run.IsFinalPot = true;
        SaveSerializer.Apply(ok, new GameState(), new MetaState());
    }

    static void Test_Corrupt_NegativeBottomFlavor_Throws()
    {
        var data = new SaveData();
        data.Bottom.Flavors[FlavorType.Sweet.ToString()] = -1;
        AssertThrows<InvalidDataException>(
            () => SaveSerializer.Apply(data, new GameState(), new MetaState()),
            "负锅底值应抛 InvalidDataException（不静默钳 0）");
    }

    static void Test_Corrupt_NegativeGold_Throws()
    {
        var data = new SaveData();
        data.Player.Gold = -1;
        AssertThrows<InvalidDataException>(
            () => SaveSerializer.Apply(data, new GameState(), new MetaState()),
            "负金币应抛 InvalidDataException");
    }

    /// <summary>空白 RouteId 归一化为 null，与 ProfessionId 一致。</summary>
    static void Test_BlankRouteId_NormalizedToNull()
    {
        var data = new SaveData();
        data.Run.RouteId = "   ";

        var target = new GameState();
        SaveSerializer.Apply(data, target, new MetaState());

        Assert(target.Run.RouteId == null,
            $"空白 RouteId 应归一化为 null，实际 '{target.Run.RouteId}'");
    }

    // ── 6. 局外仙丹粉末跨存档往返 ─────────────────────────────────────────────
    static void Test_MetaPowder_RoundTrip_And_Cleared()
    {
        var meta = new MetaState();
        meta.TryAddImmortalPowder(new ItemInstance(ItemData.ImmortalPowder, "powder-roundtrip-1"));

        var state = new GameState();
        string json = SaveSerializer.ToJson(SaveSerializer.Capture(state, meta));

        // 往返后：局外粉末保留，且是同一 InstanceId。
        var restoredMeta = new MetaState();
        SaveSerializer.Apply(SaveSerializer.FromJson(json), new GameState(), restoredMeta);
        Assert(restoredMeta.ImmortalPowderCount == 1, "往返后局外应保留 1 份仙丹粉末");
        Assert(restoredMeta.ImmortalPowders[0].InstanceId == "powder-roundtrip-1",
            "往返后仙丹粉末 InstanceId 应一致");
        Assert(restoredMeta.ImmortalPowders[0].Definition.Id == ItemData.ImmortalPowderId,
            "往返后仙丹粉末 Definition 应为 immortal_powder");

        // Apply 一份「无粉末」存档到已有粉末的 Meta 上：应被清空（幂等 / 不残留）。
        var dirtyMeta = new MetaState();
        dirtyMeta.TryAddImmortalPowder(ItemData.CreateImmortalPowder());
        SaveSerializer.Apply(new SaveData(), new GameState(), dirtyMeta);
        Assert(dirtyMeta.ImmortalPowderCount == 0, "Apply 无粉末存档后局外粉末应被清空");
    }
}
