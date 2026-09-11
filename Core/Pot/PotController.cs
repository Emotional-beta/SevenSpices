using SevenSpices.Core.Game;

namespace SevenSpices.Core.Pot;

/// <summary>
/// 负责一锅的基础流程：开锅、开碗、推进阶段、进入下一碗、结束锅。
/// 只控制流程，不保存业务状态（状态由 GameState / PotState 持有）。
/// </summary>
public class PotController
{
    private readonly GameState _gameState;

    public PotController(GameState gameState)
    {
        _gameState = gameState;
    }

    public GameState GameState => _gameState;
    public PotState Pot => _gameState.Pot;

    /// <summary>
    /// 开始一锅：将锅标记为进行中，注入锅底，碗数归 1。
    /// 只能在 PotPhase.NotStarted 时调用。
    /// </summary>
    public void StartPot()
    {
        var pot = _gameState.Pot;
        if (pot.Phase != PotPhase.NotStarted)
            throw new InvalidOperationException($"Cannot start pot: pot phase is already {pot.Phase}.");

        pot.BowlNumber = 1;
        pot.Phase = PotPhase.InProgress;
        _gameState.Bottom.ApplyToPot(pot);
    }

    /// <summary>
    /// 开始当前碗：重置碗内分数与锁定状态，进入 BowlPhase.Start。
    /// 只能在 PotPhase.InProgress 时调用。
    /// </summary>
    public void StartBowl()
    {
        var pot = _gameState.Pot;
        if (pot.Phase != PotPhase.InProgress)
            throw new InvalidOperationException($"Cannot start bowl: pot phase is {pot.Phase}.");

        pot.BaseScore = 0;
        pot.FinalScore = 0;
        pot.IsScoreLocked = false;
        pot.CurrentBowlPhase = BowlPhase.Start;
    }

    /// <summary>
    /// 推进当前碗至下一阶段。
    /// 当碗到达 BowlPhase.End 且已达碗数上限时，锅自动进入 PotPhase.Ended。
    /// </summary>
    public void AdvanceBowlPhase()
    {
        var pot = _gameState.Pot;
        if (pot.Phase != PotPhase.InProgress)
            throw new InvalidOperationException($"Cannot advance bowl phase: pot phase is {pot.Phase}.");
        if (pot.CurrentBowlPhase == BowlPhase.End)
            throw new InvalidOperationException("Current bowl is already at End. Call StartNextBowl(), or check if the pot has ended.");

        pot.CurrentBowlPhase = (BowlPhase)((int)pot.CurrentBowlPhase + 1);

        if (pot.CurrentBowlPhase == BowlPhase.End && pot.BowlNumber >= pot.BowlLimit)
            pot.Phase = PotPhase.Ended;
    }

    /// <summary>
    /// 推进到下一碗：碗数 +1，重置碗状态，进入 BowlPhase.Start。
    /// 要求当前碗已到达 BowlPhase.End 且锅仍处于 InProgress（即未到上限）。
    /// </summary>
    public void StartNextBowl()
    {
        var pot = _gameState.Pot;
        if (pot.Phase != PotPhase.InProgress)
            throw new InvalidOperationException($"Cannot start next bowl: pot phase is {pot.Phase}.");
        if (pot.CurrentBowlPhase != BowlPhase.End)
            throw new InvalidOperationException($"Cannot start next bowl: current bowl phase is {pot.CurrentBowlPhase}.");

        pot.BowlNumber++;
        StartBowl();
    }

    /// <summary>
    /// 强制结束当前锅（供特殊游戏机制使用）。
    /// 仅在 PotPhase.InProgress 时有效。
    /// </summary>
    public void EndPot()
    {
        var pot = _gameState.Pot;
        if (pot.Phase != PotPhase.InProgress)
            throw new InvalidOperationException($"Cannot end pot: pot phase is {pot.Phase}.");
        pot.Phase = PotPhase.Ended;
    }
}
