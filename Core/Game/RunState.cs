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

    /// <summary>章末 Boss（饕餮）与最终锅真身的验收记录（按触发顺序）。</summary>
    public List<ChapterBossRecord> ChapterBossRecords { get; } = new();

    /// <summary>
    /// 本局是否已终止（章末被饕餮嫌弃，失败收口，投胎重来）。
    /// <para>
    /// 与 <see cref="Outcome"/> 不同源：本值描述「章末 Boss 嫌弃导致的本局终止」，
    /// 而 <see cref="Outcome"/> 只描述「最终锅真身试吃」的结局。表现层应先判本值——
    /// 若为 true 则本局已被吞、不会进入最终锅结算（此时 <see cref="Outcome"/> 仍为
    /// <see cref="RunOutcome.Unsettled"/>）；否则再读 <see cref="Outcome"/>。
    /// </para>
    /// </summary>
    public bool IsFailed { get; set; }

    /// <summary>本局终止原因 / 保底评价文案（未终止时为 null）。</summary>
    public string? FailReason { get; set; }

    /// <summary>
    /// 本局结局（最终锅真身试吃的判定结果）；新局开始时重置为 Unsettled。
    /// <para>
    /// 仅当本局走到最终锅真身试吃时才会被写入。判断整体本局结果时须先看
    /// <see cref="IsFailed"/>（章末失败优先），未失败再读本值。
    /// </para>
    /// </summary>
    public RunOutcome Outcome { get; set; } = RunOutcome.Unsettled;
}
