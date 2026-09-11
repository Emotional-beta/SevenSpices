namespace SevenSpices.Core.Game;

/// <summary>
/// 整个游戏运行状态的根。
/// 负责保存状态，不负责承担复杂的游戏流程逻辑。
/// </summary>
public class GameState
{
    public PlayerState Player { get; } = new();
    public RunState Run { get; } = new();
    public PotState Pot { get; } = new();
    public BottomState Bottom { get; } = new();
    public CustomerState Customer { get; } = new();
}
