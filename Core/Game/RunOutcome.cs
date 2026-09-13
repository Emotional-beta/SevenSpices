namespace SevenSpices.Core.Game;

/// <summary>
/// 本局结局：只描述<b>最终锅真身试吃</b>的判定结果（Okay = 尚可 / Restart = 重来）。
/// <para>
/// 章末 Boss 嫌弃造成的本局终止由 <see cref="RunState.IsFailed"/> 表示，与本枚举不同源、
/// 需分别判定：章末失败时 <see cref="RunState.IsFailed"/> 为 true，而本枚举仍为
/// <see cref="Unsettled"/>（本局并未走到最终锅真身试吃）。
/// </para>
/// </summary>
public enum RunOutcome
{
    /// <summary>尚未结算。</summary>
    Unsettled,

    /// <summary>尚可：真身满意，赏下仙丹粉末；随后仍依天规被吞，投胎重来。</summary>
    Okay,

    /// <summary>重来：真身嫌弃。</summary>
    Restart,
}
