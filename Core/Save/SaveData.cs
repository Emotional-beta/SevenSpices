namespace SevenSpices.Core.Save;

/// <summary>
/// 存档根 DTO（架构文档 §25）。只承载「游戏状态」，不承载任何 Godot 场景 / 运行时对象。
/// <para>
/// 全部成员均为默认构造函数 + 公开 get/set，便于 <c>System.Text.Json</c> 直接序列化。
/// <see cref="Version"/> 为将来的存档迁移预留，V1 固定为 <see cref="SaveSerializer.CurrentVersion"/>。
/// </para>
/// </summary>
public sealed class SaveData
{
    /// <summary>存档格式版本；V1 = 1。</summary>
    public int Version { get; set; } = SaveSerializer.CurrentVersion;

    /// <summary>本局进度。</summary>
    public RunStateDto Run { get; set; } = new();

    /// <summary>玩家长期资源（金币 / 食材篮 / 道具 / 伙伴）。</summary>
    public PlayerStateDto Player { get; set; } = new();

    /// <summary>锅底长期味道积累。</summary>
    public BottomStateDto Bottom { get; set; } = new();

    /// <summary>局外（跨局）保留状态。</summary>
    public MetaStateDto Meta { get; set; } = new();

    /// <summary>已解锁内容（V1 恒空，为局外解锁预留）。</summary>
    public List<string> UnlockedContent { get; set; } = new();

    /// <summary>设置（V1 恒为 null，为音量 / 画质等预留）。</summary>
    public Dictionary<string, string>? Settings { get; set; }
}

/// <summary>本局进度 DTO，对应 <c>RunState</c>。</summary>
public sealed class RunStateDto
{
    public int Chapter { get; set; } = 1;
    public int PotIndex { get; set; } = 1;
    public bool IsFinalPot { get; set; }
    public string? RouteId { get; set; }
    public int RouteActiveChapter { get; set; }
    public string? ProfessionId { get; set; }
    public bool IsFailed { get; set; }
    public string? FailReason { get; set; }

    /// <summary>本局结局（枚举名字符串，如 <c>Unsettled</c> / <c>Okay</c> / <c>Restart</c>）。</summary>
    public string Outcome { get; set; } = "Unsettled";

    /// <summary>饕餮验收记录（按触发顺序）。</summary>
    public List<BossRecordDto> ChapterBossRecords { get; set; } = new();
}

/// <summary>饕餮一次试吃的验收记录 DTO，对应 <c>ChapterBossRecord</c>。</summary>
public sealed class BossRecordDto
{
    public int Chapter { get; set; }
    public int PotIndex { get; set; }
    public string BossId { get; set; } = string.Empty;
    public string BossName { get; set; } = string.Empty;
    public bool Satisfied { get; set; }
    public int PotTotalFinalScore { get; set; }
    public int Threshold { get; set; }
    public bool IsFinalPot { get; set; }
}

/// <summary>玩家长期资源 DTO，对应 <c>PlayerState</c>。</summary>
public sealed class PlayerStateDto
{
    public int Gold { get; set; }
    public List<InstanceDto> IngredientBasket { get; set; } = new();
    public List<InstanceDto> Items { get; set; } = new();
    public List<InstanceDto> Companions { get; set; } = new();
}

/// <summary>通用实例 DTO：只保存实例唯一 Id 与静态定义 Id，读档时按 Id 从 Registry 找回定义。</summary>
public sealed class InstanceDto
{
    public string InstanceId { get; set; } = string.Empty;
    public string DefinitionId { get; set; } = string.Empty;
}

/// <summary>锅底状态 DTO，对应 <c>BottomState</c>；味道以 <c>FlavorType</c> 名称为键。</summary>
public sealed class BottomStateDto
{
    public Dictionary<string, int> Flavors { get; set; } = new();
}

/// <summary>局外（跨局）保留状态 DTO，对应 <c>MetaState</c>（当前仅仙丹粉末）。</summary>
public sealed class MetaStateDto
{
    public List<InstanceDto> ImmortalPowders { get; set; } = new();
}
