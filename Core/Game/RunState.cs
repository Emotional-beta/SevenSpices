namespace SevenSpices.Core.Game;

/// <summary>
/// 当前游戏局的整体进度状态（章节、锅序号、路线）。
/// </summary>
public class RunState
{
    /// <summary>当前章节（1-3）。</summary>
    public int Chapter { get; set; } = 1;

    /// <summary>本章第几锅（1-3）。</summary>
    public int PotIndex { get; set; } = 1;

    /// <summary>是否进入最终锅。</summary>
    public bool IsFinalPot { get; set; }

    /// <summary>当前选择的路线 ID（Phase 4 填充）。</summary>
    public string? RouteId { get; set; }

    /// <summary>章末 Boss（饕餮）验收记录（按触发顺序）。</summary>
    public List<ChapterBossRecord> ChapterBossRecords { get; } = new();

    /// <summary>本局是否已终止（章末被饕餮嫌弃，失败收口，投胎重来）。</summary>
    public bool IsFailed { get; set; }

    /// <summary>本局终止原因 / 保底评价文案（未终止时为 null）。</summary>
    public string? FailReason { get; set; }
}
