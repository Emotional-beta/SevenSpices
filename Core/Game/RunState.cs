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

    /// <summary>
    /// 当前生效的餐饮风潮 Id（对应 <c>RouteConfig</c> 的风潮池）；无风潮时为 null。
    /// <para>
    /// 只有 <c>RouteKind.FlavorTrend</c> 路线会写入本字段；保底路线立即发放收益，
    /// 不写风潮状态。到期（跨过 <see cref="RouteActiveChapter"/>）后由 GameController 清除。
    /// </para>
    /// </summary>
    public string? RouteId { get; set; }

    /// <summary>
    /// 当前风潮生效的章节（无风潮时为 0）。
    /// 选择风潮时写入「生效章节」，<c>AdvanceToNextPot</c> 推进章节后若已越过则清除风潮。
    /// </summary>
    public int RouteActiveChapter { get; set; }

    /// <summary>
    /// 本局当前风潮是否由「第 3 章末的选择」写入、目标是最终锅。
    /// <para>
    /// 用于区分「第 3 章末选风潮」（应保留到最终锅）与「第 2 章末选风潮」（生效章节同样为 3，
    /// 但只应作用于第 3 章普通锅）。仅当为 true 时，推进到最终锅才保留风潮；否则清除。
    /// </para>
    /// </summary>
    public bool RouteTargetsFinalPot { get; set; }

    /// <summary>
    /// 本局选择的职业 ID（对应 <c>ProfessionConfig</c>）；新局开始时由 GameController 写入，
    /// <c>RunController.StartRun</c> 复位为 null。供起始套装与起手规则读取。
    /// </summary>
    public string? ProfessionId { get; set; }

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
