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
}
