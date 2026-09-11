using SevenSpices.Core.Game;
using SevenSpices.Core.Pot;

namespace SevenSpices.Core.Run;

/// <summary>
/// 负责 Run 层的 Chapter / Pot 推进流程。
/// 管理 RunState 的状态变化，并负责创建和启动每一锅的 PotController。
/// 不负责碗内流程、食客、道具、商店、存档等具体内容。
/// </summary>
public class RunController
{
    /// <summary>每局游戏的章节总数。</summary>
    public const int ChaptersPerRun = 3;

    /// <summary>每章的普通锅总数。</summary>
    public const int PotsPerChapter = 3;

    private readonly GameState _gameState;

    public RunController(GameState gameState)
    {
        ArgumentNullException.ThrowIfNull(gameState);
        _gameState = gameState;
    }

    public GameState GameState => _gameState;
    public RunState Run => _gameState.Run;

    /// <summary>当前锅是否已完成（含最终锅）。</summary>
    public bool IsCurrentPotEnded => _gameState.Pot.Phase == PotPhase.Ended;

    /// <summary>
    /// 整局游戏是否已完成：最终锅已结束。
    /// </summary>
    public bool IsRunComplete =>
        _gameState.Run.IsFinalPot && _gameState.Pot.Phase == PotPhase.Ended;

    /// <summary>
    /// 开始一局新游戏：将 RunState 重置为初始状态。
    /// 不启动锅，调用方需在此后调用 StartCurrentPot()。
    /// </summary>
    public void StartRun()
    {
        var run = _gameState.Run;
        run.Chapter = 1;
        run.PotIndex = 1;
        run.IsFinalPot = false;
        run.RouteId = null;
    }

    /// <summary>
    /// 根据当前 RunState 启动对应的锅：重置 PotState，注入锅底，返回已启动的 PotController。
    /// 普通锅 BowlLimit = 10，最终锅 BowlLimit = int.MaxValue。
    /// </summary>
    public PotController StartCurrentPot()
    {
        var run = _gameState.Run;
        int bowlLimit = run.IsFinalPot ? int.MaxValue : 10;
        _gameState.Pot.Reset(bowlLimit);
        var ctrl = new PotController(_gameState);
        ctrl.StartPot();
        return ctrl;
    }

    /// <summary>
    /// 当前普通锅结束后，推进到下一锅或进入最终锅。
    /// 要求：当前锅已结束（PotPhase.Ended），且当前非最终锅。
    /// 推进规则：
    ///   PotIndex &lt; PotsPerChapter → PotIndex++
    ///   Chapter  &lt; ChaptersPerRun → Chapter++, PotIndex = 1
    ///   否则 → IsFinalPot = true
    /// </summary>
    public void AdvanceToNextPot()
    {
        var run = _gameState.Run;
        if (run.IsFinalPot)
            throw new InvalidOperationException(
                "Cannot advance: already in Final Pot. The run ends when Final Pot is completed.");

        if (_gameState.Pot.Phase != PotPhase.Ended)
            throw new InvalidOperationException(
                $"Cannot advance: current pot phase is {_gameState.Pot.Phase}, expected Ended.");

        if (run.PotIndex < PotsPerChapter)
        {
            run.PotIndex++;
        }
        else if (run.Chapter < ChaptersPerRun)
        {
            run.Chapter++;
            run.PotIndex = 1;
        }
        else
        {
            run.IsFinalPot = true;
        }
    }
}
