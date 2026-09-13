using SevenSpices.Core.Companions;
using SevenSpices.Core.Content;
using SevenSpices.Core.Customers;
using SevenSpices.Core.Game;
using SevenSpices.Core.Ingredients;
using SevenSpices.Core.Items;
using SevenSpices.Core.Pot;
using SevenSpices.Core.Scoring;

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
    private readonly CompanionSystem? _companions;

    /// <summary>味道系统可调数值配置，透传给每一锅的 PotController。为 null 时使用 <see cref="FlavorConfig.Default"/>。</summary>
    private readonly FlavorConfig? _flavorConfig;

    public RunController(GameState gameState, CompanionSystem? companions = null, FlavorConfig? flavorConfig = null)
    {
        ArgumentNullException.ThrowIfNull(gameState);
        _gameState = gameState;
        _companions = companions;
        _flavorConfig = flavorConfig;
    }

    public GameState GameState => _gameState;
    public RunState Run => _gameState.Run;

    /// <summary>当前锅是否已完成（含最终锅）。</summary>
    public bool IsCurrentPotEnded => _gameState.Pot.Phase == PotPhase.Ended;

    /// <summary>
    /// 整局游戏是否已完成：最终锅已结束，或本局已因章末被嫌弃而终止。
    /// </summary>
    public bool IsRunComplete =>
        _gameState.Run.IsFailed
        || (_gameState.Run.IsFinalPot && _gameState.Pot.Phase == PotPhase.Ended);

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
        run.ChapterBossRecords.Clear();
        run.IsFailed = false;
        run.FailReason = null;
    }

    /// <summary>
    /// 本局终止（失败收口）：章末 Boss（饕餮）嫌弃，当场被吞，投胎重来。
    /// 终止后本局不可再推进锅；调用方据此结束本局。
    /// </summary>
    /// <param name="reason">终止原因 / 保底评价文案；空白抛 <see cref="ArgumentException"/>。</param>
    public void FailRun(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("FailRun reason cannot be empty.", nameof(reason));

        if (_gameState.Run.IsFailed)
            throw new InvalidOperationException("Cannot fail run: the run has already failed.");

        _gameState.Run.IsFailed = true;
        _gameState.Run.FailReason = reason;
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
        var ctrl = new PotController(_gameState, _companions, flavorConfig: _flavorConfig);
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
        if (run.IsFailed)
            throw new InvalidOperationException(
                "Cannot advance: the run has failed. Fresh pot is not allowed after a failed run.");

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

    /// <summary>
    /// 最终锅结算入口：玩家主动结束煮粥，触发食客评价、奖励派发、锅底提炼。
    /// 只能在 IsFinalPot == true 且锅仍在进行中（PotPhase.InProgress）时调用。
    /// 重复调用会因锅的阶段检查而抛出异常，天然防止奖励重复发放。
    /// </summary>
    /// <param name="customer">本次结算的食客实例。</param>
    /// <param name="baseGoldReward">普通食客喝粥后的基础金币奖励。</param>
    /// <param name="rewardIngredient">稀有食客满意时的食材奖励（可为 null）。</param>
    /// <param name="rewardItem">稀有食客满意时的道具奖励（可为 null）。</param>
    public void EndCooking(
        CustomerInstance customer,
        int baseGoldReward = 0,
        IngredientInstance? rewardIngredient = null,
        ItemInstance? rewardItem = null)
    {
        ArgumentNullException.ThrowIfNull(customer);

        if (_gameState.Run.IsFailed)
            throw new InvalidOperationException(
                "Cannot end cooking: the run has already failed.");

        if (!_gameState.Run.IsFinalPot)
            throw new InvalidOperationException(
                "EndCooking can only be called during Final Pot.");

        var pot = _gameState.Pot;
        if (pot.Phase != PotPhase.InProgress)
            throw new InvalidOperationException(
                $"Cannot end cooking: pot phase is {pot.Phase}, expected InProgress.");

        // 最终锅整锅一次性结算：先按 ×32 计算并锁定最终分数，再结束锅。
        // 顺序很重要：分数若已被锁定，CalculateAndLock 会抛异常，此时必须零副作用，
        // 否则会留下「锅已 Ended 但奖励未发」且无法重试的死局。
        // 同时 SatisfactionEvaluator 读的就是 FinalScore，算分必须发生在 EvaluateAndReward 之前。
        ScoreCalculator.CalculateAndLock(pot);

        var ctrl = new PotController(_gameState, _companions, flavorConfig: _flavorConfig);
        ctrl.EndPot();

        CustomerService.AssignCustomer(_gameState.Customer, customer);
        CustomerService.EvaluateAndReward(
            _gameState.Customer, _gameState.Pot, _gameState.Player,
            baseGoldReward, rewardIngredient, rewardItem);

        ctrl.ClosePot();
    }
}
