using SevenSpices.Core.Content;
using SevenSpices.Core.Customers;
using SevenSpices.Core.Effects;
using SevenSpices.Core.Events;
using SevenSpices.Core.Ingredients;
using SevenSpices.Core.Items;
using SevenSpices.Core.Pot;
using SevenSpices.Core.Run;
using SevenSpices.Core.Scoring;

namespace SevenSpices.Core.Game;

/// <summary>
/// 一局游戏对表现层的统一入口（Game Layer）。
/// 持有 GameState + RunController + EffectSystem，封装游戏流程阶段的推进与玩家动作。
/// 每锅开始时从「当前食材篮」生成一份本锅可抽池（快照，不修改篮）；
/// 选中的实例从本锅池移出，未选中的回池；锅结束后本锅池作废，篮不变。
/// 纯 C#，不依赖 Godot / 场景节点。
/// </summary>
public class GameController
{
    private const int DrawCount = 3;

    private readonly GameState _state;
    private readonly RunController _runController;
    private readonly EventBus _events;
    private readonly EffectSystem _effectSystem;
    private readonly EffectSystem _previewEffectSystem = new();
    private readonly CustomerAppearanceConfig _appearance;
    private readonly PotRewardConfig _potReward;
    private readonly Random _random;
    private PotController? _potController;
    private IngredientPool? _pool;
    private readonly List<IngredientInstance> _candidates = new();
    private readonly List<IngredientInstance> _rewardCandidates = new();
    private bool _rewardResolved;

    /// <param name="state">可注入已有 GameState（测试 / 存档恢复用）；为空则新建。</param>
    /// <param name="appearance">食客出现机制配置；为空则使用默认配置。</param>
    /// <param name="random">随机源（食客出现、稀有掉落、锅结束奖励抽取）；为空则使用 <see cref="Random.Shared"/>。</param>
    /// <param name="potReward">锅结束奖励配置（X 选 1）；为空则使用默认配置。</param>
    public GameController(
        GameState? state = null,
        CustomerAppearanceConfig? appearance = null,
        Random? random = null,
        PotRewardConfig? potReward = null)
    {
        _state = state ?? new GameState();
        _runController = new RunController(_state);
        _events = new EventBus();
        _effectSystem = new EffectSystem(_events);
        _appearance = appearance ?? new CustomerAppearanceConfig();
        _potReward = potReward ?? new PotRewardConfig();
        _random = random ?? Random.Shared;
    }

    /// <summary>游戏运行状态根。</summary>
    public GameState State => _state;

    /// <summary>本局的只读事件总线，供表现层 / 伙伴系统等订阅刷新。</summary>
    public EventBus Events => _events;

    public RunState Run => _state.Run;
    public PotState Pot => _state.Pot;
    public PlayerState Player => _state.Player;

    /// <summary>玩家当前持有的道具实例（长期资源，跨碗/跨锅保留）。只读。</summary>
    public IReadOnlyList<ItemInstance> Items => _state.Player.Items;

    /// <summary>当前碗的食客实例（null 表示尚未指派或已结算）。只读。</summary>
    public CustomerInstance? CurrentCustomer => _state.Customer.CurrentCustomer;

    /// <summary>当前是否处于最终锅。</summary>
    public bool IsFinalPot => _state.Run.IsFinalPot;

    /// <summary>
    /// 是否允许推进到下一锅：当前普通锅已 Ended，且锅结束奖励已处理
    /// （已选定，或本锅本就未产生候选）。
    /// 最终锅只能通过 <see cref="EndCooking"/> 结束整局，不能推进。
    /// <para>
    /// 防御存档恢复：构造函数声明可注入已有 GameState（存档恢复用），此时奖励态字段
    /// （_rewardResolved / _rewardCandidates）为默认值。若该锅结束时未产生候选，
    /// 仅靠 _rewardResolved 会在恢复后卡死；追加「无候选即放行」可避免这种双 false 软锁。
    /// </para>
    /// </summary>
    public bool CanAdvanceToNextPot =>
        _state.Pot.Phase == PotPhase.Ended
        && !_state.Run.IsFinalPot
        && (_rewardResolved || _rewardCandidates.Count == 0);

    /// <summary>普通锅结束时的 X 选 1 奖励候选（未进入奖励态时为空）。只读。</summary>
    public IReadOnlyList<IngredientInstance> RewardCandidates => _rewardCandidates;

    /// <summary>
    /// 是否正在等待玩家选择锅结束奖励：普通锅、锅已 Ended、尚未选定、且有候选。
    /// </summary>
    public bool IsAwaitingReward =>
        !_state.Run.IsFinalPot
        && _state.Pot.Phase == PotPhase.Ended
        && !_rewardResolved
        && _rewardCandidates.Count > 0;

    /// <summary>兼容别名：语义等同 <see cref="IsAwaitingReward"/>，供表现层按钮守卫使用。</summary>
    public bool CanChooseReward => IsAwaitingReward;

    /// <summary>锅结束奖励的候选数量 X，供 UI 文案使用。</summary>
    public int RewardChoiceCount => _potReward.ChoiceCount;

    /// <summary>整局是否已完成：最终锅已结束。委托领域层，语义等价。最终锅结算前恒为 false。</summary>
    public bool IsRunComplete => _runController.IsRunComplete;

    /// <summary>
    /// 当前是否允许结束煮粥：处于最终锅且锅正在（InProgress）。
    /// 集中此判定，供表现层按钮守卫与禁用态统一使用。
    /// </summary>
    public bool CanEndCooking =>
        _state.Run.IsFinalPot && _state.Pot.Phase == PotPhase.InProgress;

    /// <summary>当前抽屉中的候选食材实例（最多 3 个，池不足则有几个抽几个）。</summary>
    public IReadOnlyList<IngredientInstance> CurrentCandidates => _candidates;

    /// <summary>本锅可抽取食材总数：本锅池中剩余实例 + 当前抽屉中的候选。</summary>
    public int RemainingPoolCount => (_pool?.Count ?? 0) + _candidates.Count;

    /// <summary>当前是否允许选择食材：锅进行中、处于 IngredientSelection 且有候选。</summary>
    public bool CanSelectIngredient =>
        _state.Pot.Phase == PotPhase.InProgress
        && _state.Pot.CurrentBowlPhase == BowlPhase.IngredientSelection
        && _candidates.Count > 0;

    /// <summary>
    /// 当前是否允许跳过本碗：普通锅、锅进行中、处于 IngredientSelection 且池已空（无候选）。
    /// 池空兜底，保证普通锅 10 碗流程能正常走完（§5.2）。
    /// </summary>
    public bool CanSkipBowl =>
        !_state.Run.IsFinalPot
        && _state.Pot.Phase == PotPhase.InProgress
        && _state.Pot.CurrentBowlPhase == BowlPhase.IngredientSelection
        && _candidates.Count == 0;

    /// <summary>兼容旧命名：语义等同 <see cref="CanSelectIngredient"/>。</summary>
    public bool CanAddIngredient => CanSelectIngredient;

    /// <summary>
    /// 开始一局新游戏：启动 Run；若食材篮为空则填入初始套装；
    /// 若尚无道具则随机给 1 个初始道具；随后启动第 1 锅。
    /// </summary>
    public void StartNewGame()
    {
        _runController.StartRun();

        if (_state.Player.IngredientBasket.Count == 0)
            _state.Player.IngredientBasket.AddRange(IngredientData.CreateInitialBasket());

        if (_state.Player.Items.Count == 0)
            _state.Player.Items.AddRange(ItemData.CreateInitialItems(_random));

        StartCurrentPot();
    }

    /// <summary>
    /// 当前是否允许使用道具：非最终锅、锅进行中、处于「加入食材之前」的
    /// ItemPhase 或 IngredientSelection，且玩家持有道具（设计文档 §十五）。
    /// </summary>
    public bool CanUseItem =>
        !IsFinalPot
        && _state.Pot.Phase == PotPhase.InProgress
        && (_state.Pot.CurrentBowlPhase == BowlPhase.ItemPhase
            || _state.Pot.CurrentBowlPhase == BowlPhase.IngredientSelection)
        && _state.Player.Items.Count > 0;

    /// <summary>
    /// 使用一个道具：消耗实例并即时触发其效果链。
    /// 只能在 <see cref="CanUseItem"/> 为真时调用。
    /// <para>
    /// 道具带来的分数变化会同步计入本锅累计基础分（<see cref="PotState.TotalBaseScore"/>），
    /// 避免累计值漏记道具加分（如食盐 +3）。
    /// </para>
    /// </summary>
    public void UseItem(string instanceId)
    {
        if (!CanUseItem)
            throw new InvalidOperationException(
                $"Cannot use item: final={_state.Run.IsFinalPot}, pot={_state.Pot.Phase}, " +
                $"bowl={_state.Pot.CurrentBowlPhase}, items={_state.Player.Items.Count}.");

        var potController = EnsurePotController();

        int before = _state.Pot.BaseScore;
        var item = potController.UseItem(instanceId);
        potController.ApplyItemEffect(item, _effectSystem);
        _state.Pot.TotalBaseScore += _state.Pot.BaseScore - before;

        _events.Publish(new ItemUsedEvent(item));
    }

    /// <summary>
    /// 启动当前 Run 状态下的一锅：
    /// 建本锅池（当前食材篮的快照）→ 开始第 1 碗 → 推进到 IngredientSelection → 抽取候选。
    /// </summary>
    public void StartCurrentPot()
    {
        // 食客状态按「本锅」语义重置：必须在建池 / 指派食客之前。
        _state.Customer.ResetForPot();

        _potController = _runController.StartCurrentPot();
        _events.Publish(new PotStartedEvent(
            _state.Run.Chapter, _state.Run.PotIndex, _state.Run.IsFinalPot));

        _potController.StartBowl();
        _events.Publish(new BowlStartedEvent(_state.Pot.BowlNumber));

        _pool = new IngredientPool(_state.Player.IngredientBasket, _random);
        _candidates.Clear();

        // 奖励状态按「本锅」重置：候选与已选定标记都属于上一锅的收尾。
        _rewardCandidates.Clear();
        _rewardResolved = false;

        // 最终锅不逐碗指派食客：整锅由 EndCooking 用占位食客一次性结算。
        if (!_state.Run.IsFinalPot)
            AssignCustomerForBowl();

        AdvanceToIngredientSelection(_potController);

        DrawCandidates();
        _events.Publish(new IngredientDrawnEvent(_candidates));
    }

    /// <summary>
    /// 当前锅结束后推进到下一锅（或最终锅），并立即启动该锅。
    /// 要求 <see cref="CanAdvanceToNextPot"/> 为真；锅底提炼已在锅结束时完成，此处不重复提炼。
    /// </summary>
    public void AdvanceToNextPot()
    {
        if (!CanAdvanceToNextPot)
            throw new InvalidOperationException(
                $"Cannot advance to next pot: pot={_state.Pot.Phase}, final={_state.Run.IsFinalPot}.");

        _runController.AdvanceToNextPot();
        StartCurrentPot();
    }

    /// <summary>
    /// 选择并投入当前候选中的一个实例。
    /// 选中的实例从本锅池移出，未选中的自动回池；随后结算并推进本碗流程。
    /// 只能在 <see cref="CanSelectIngredient"/> 为真时调用。
    /// </summary>
    public void SelectIngredient(string instanceId)
    {
        if (!CanSelectIngredient)
            throw new InvalidOperationException(
                $"Cannot select ingredient: pot={_state.Pot.Phase}, bowl={_state.Pot.CurrentBowlPhase}, candidates={_candidates.Count}.");

        var pool = _pool ?? throw new InvalidOperationException("Ingredient pool is not initialized.");
        var potController = EnsurePotController();

        var chosen = pool.Confirm(_candidates, instanceId);
        _candidates.Clear();

        // IngredientSelection → IngredientResolve
        potController.AdvanceBowlPhase();
        potController.AddIngredient(chosen, _effectSystem);
        _events.Publish(new IngredientAddedEvent(chosen));

        FinishBowlAfterIngredient();
    }

    /// <summary>
    /// 跳过本碗（池空兜底）：不投入食材，直接走完本碗。
    /// 只能在 <see cref="CanSkipBowl"/> 为真时调用。
    /// </summary>
    public void SkipBowl()
    {
        if (!CanSkipBowl)
            throw new InvalidOperationException(
                $"Cannot skip bowl: pot={_state.Pot.Phase}, bowl={_state.Pot.CurrentBowlPhase}, final={_state.Run.IsFinalPot}, candidates={_candidates.Count}.");

        FinishBowl();
    }

    /// <summary>
    /// 选定普通锅结束奖励：将选中的候选实例直接加入食材篮，成为跨锅可复用资源。
    /// 只能在 <see cref="CanChooseReward"/> 为真时调用；选定后 <see cref="CanAdvanceToNextPot"/> 才为真。
    /// </summary>
    public void ChooseReward(string instanceId)
    {
        if (!CanChooseReward)
            throw new InvalidOperationException(
                $"Cannot choose reward: pot={_state.Pot.Phase}, final={_state.Run.IsFinalPot}, resolved={_rewardResolved}, candidates={_rewardCandidates.Count}.");

        var chosen = _rewardCandidates.FirstOrDefault(r => r.InstanceId == instanceId)
            ?? throw new ArgumentException($"Reward candidate '{instanceId}' not found.", nameof(instanceId));

        _state.Player.IngredientBasket.Add(chosen);
        _rewardCandidates.Clear();
        _rewardResolved = true;

        _events.Publish(new RewardChosenEvent(chosen));
    }

    /// <summary>
    /// 预测将候选实例投入当前碗后的本碗基础分，不修改任何真实状态。
    /// 可在 IngredientSelection 阶段（悬停候选时）调用。
    /// </summary>
    public IngredientPreview PreviewIngredient(IngredientInstance candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        // 预览在快照上执行效果，属于只读推演，不应发布 EffectTriggeredEvent，故使用无总线的 EffectSystem。
        return EnsurePotController().PreviewIngredient(candidate, _previewEffectSystem);
    }

    /// <summary>
    /// 结束煮粥：结算最终锅。只能在最终锅且锅进行中调用。
    /// 食客与奖励规则尚未接入内容层，暂用正式普通食客数据占位（保持现状）。
    /// </summary>
    public void EndCooking()
    {
        var customer = CustomerData.CreateNormalInstance();
        _runController.EndCooking(customer);
        _candidates.Clear();
        _pool = null;
        _rewardCandidates.Clear();
        _rewardResolved = false;

        // 最终锅由 RunController 内部一次性结算；此处只读取已结算的状态发布事件。
        var pot = _state.Pot;
        int multiplier = ScoreCalculator.GetMultiplier(pot.BowlNumber);
        _events.Publish(new ScoreCalculatedEvent(pot.BaseScore, pot.FinalScore, multiplier));
        _events.Publish(new ScoreLockedEvent(pot.FinalScore));
        _events.Publish(new CustomerServedEvent(customer, 0));
        _events.Publish(new PotEndedEvent(
            _state.Run.Chapter, _state.Run.PotIndex, _state.Run.IsFinalPot));
        _events.Publish(new RunCompletedEvent());
    }

    /// <summary>
    /// 投入食材后的收尾。
    /// 最终锅不逐碗：回到选择阶段并重新抽候选（池已减少一个），保持可继续添加。
    /// 普通锅：走完本碗到 End，并进入下一碗的选择阶段重新抽候选；若本锅已 Ended 则作废本锅池。
    /// </summary>
    private void FinishBowlAfterIngredient()
    {
        if (_state.Run.IsFinalPot)
        {
            var potController = EnsurePotController();
            // 最终锅 StartBowl 不重置分数；回到 IngredientSelection 继续抽。
            potController.StartBowl();
            _events.Publish(new BowlStartedEvent(_state.Pot.BowlNumber));

            AdvanceToIngredientSelection(potController);
            DrawCandidates();
            _events.Publish(new IngredientDrawnEvent(_candidates));
            return;
        }

        FinishBowl();
    }

    /// <summary>
    /// 从当前碗阶段（IngredientSelection 或 IngredientResolve）走完本碗到 End。
    /// 普通锅接着进入下一碗的 IngredientSelection 并抽候选；锅 Ended 则作废本锅池。
    /// </summary>
    private void FinishBowl()
    {
        var potController = EnsurePotController();

        while (_state.Pot.CurrentBowlPhase != BowlPhase.End
               && _state.Pot.Phase == PotPhase.InProgress)
        {
            if (_state.Pot.CurrentBowlPhase == BowlPhase.ScoreCalculation)
            {
                potController.CalculateScore();
                PublishScoreEvents();
            }
            else if (_state.Pot.CurrentBowlPhase == BowlPhase.Serving)
            {
                ServeCurrentCustomer();
            }

            potController.AdvanceBowlPhase();
        }

        if (_state.Pot.Phase == PotPhase.InProgress)
        {
            potController.StartNextBowl();
            _events.Publish(new BowlStartedEvent(_state.Pot.BowlNumber));

            AssignCustomerForBowl();
            AdvanceToIngredientSelection(potController);

            DrawCandidates();
            _events.Publish(new IngredientDrawnEvent(_candidates));
        }
        else
        {
            // 锅结束：提炼锅底（恰好一次），本锅池与候选一并作废，篮不变。
            potController.ClosePot();
            _events.Publish(new PotEndedEvent(
                _state.Run.Chapter, _state.Run.PotIndex, _state.Run.IsFinalPot));

            _pool = null;
            _candidates.Clear();

            // 普通锅结束奖励（§17）：生成 X 个互不重复的候选，等待玩家选定。
            // 此分支只在普通锅走到（最终锅不经 FinishBowl），天然不触发最终锅奖励。
            // 显式清空，不依赖 StartCurrentPot 的隐式重置，保证本处加入前候选必为空。
            _rewardCandidates.Clear();
            _rewardCandidates.AddRange(
                IngredientData.CreateRandomInstances(_potReward.ChoiceCount, _random));

            // 边界兜底：注册表为空导致没有候选时直接视为已选定，避免卡住推进。
            // Core 层不引入 Godot / 日志，此处静默处理即可。
            if (_rewardCandidates.Count == 0)
                _rewardResolved = true;

            _events.Publish(new RewardOfferedEvent(_rewardCandidates));
        }
    }

    /// <summary>在分数计算并锁定后发布 ScoreCalculatedEvent + ScoreLockedEvent（顺序固定）。</summary>
    private void PublishScoreEvents()
    {
        var pot = _state.Pot;
        int multiplier = ScoreCalculator.GetMultiplier(pot.BowlNumber);
        _events.Publish(new ScoreCalculatedEvent(pot.BaseScore, pot.FinalScore, multiplier));
        _events.Publish(new ScoreLockedEvent(pot.FinalScore));
    }

    /// <summary>
    /// 按当前碗号为该碗指派食客（普通/稀有由 <see cref="CustomerAppearanceConfig"/> 决定）。
    /// 只在普通锅的每碗开始时调用。
    /// </summary>
    private void AssignCustomerForBowl()
    {
        var customer = _appearance.CreateCustomerForBowl(_state.Pot.BowlNumber, _random);
        CustomerService.AssignCustomer(_state.Customer, customer);
    }

    /// <summary>
    /// 食客喝粥并结算奖励。必须在分数锁定后（Serving 阶段）调用，
    /// 保证满意度读到的是已锁定的 FinalScore。
    /// 普通食客发放基础金币；稀有食客满意时掉落一个随机食材（见 CustomerService）。
    /// </summary>
    private void ServeCurrentCustomer()
    {
        var customer = _state.Customer.CurrentCustomer;
        if (customer == null)
            return;

        bool isRare = customer.Definition.IsRare;

        // 稀有食客满意时：食材 + 道具各掉落 1 个；不满意时两者都被丢弃（既有语义）。
        IngredientInstance? rewardIngredient = isRare
            ? IngredientData.CreateRandomInstance(_random)
            : null;
        ItemInstance? rewardItem = isRare
            ? ItemData.CreateRandomInstance(_random)
            : null;

        bool satisfied = CustomerService.EvaluateAndReward(
            _state.Customer,
            _state.Pot,
            _state.Player,
            _appearance.BaseGoldReward,
            rewardIngredient,
            rewardItem);

        // 普通食客获得基础金币；稀有食客不发金币（既有语义）。
        int goldAwarded = isRare ? 0 : _appearance.BaseGoldReward;
        _events.Publish(new CustomerServedEvent(customer, goldAwarded));
        if (satisfied)
            _events.Publish(new CustomerSatisfiedEvent(customer));
    }

    private void DrawCandidates()
    {
        _candidates.Clear();
        if (_pool == null)
            return;

        _candidates.AddRange(_pool.Draw(DrawCount));
    }

    /// <summary>
    /// 从碗的 Start 阶段推进到 IngredientSelection，固定 3 步契约：
    /// Start → Customer → ItemPhase → IngredientSelection。
    /// </summary>
    private static void AdvanceToIngredientSelection(PotController potController)
    {
        for (int i = 0; i < 3; i++)
            potController.AdvanceBowlPhase();
    }

    /// <summary>
    /// 获取当前锅的 PotController。
    /// 正常情况下由 StartCurrentPot 创建；若调用方注入了已进行中的 GameState
    /// （测试 / 存档恢复），则基于同一状态惰性创建，不再重复 StartPot。
    /// </summary>
    private PotController EnsurePotController()
        => _potController ??= new PotController(_state);
}
