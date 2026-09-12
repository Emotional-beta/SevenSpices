using SevenSpices.Core.Content;
using SevenSpices.Core.Customers;
using SevenSpices.Core.Effects;
using SevenSpices.Core.Ingredients;
using SevenSpices.Core.Pot;
using SevenSpices.Core.Run;

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
    private readonly EffectSystem _effectSystem;
    private readonly CustomerAppearanceConfig _appearance;
    private readonly Random _random;
    private PotController? _potController;
    private IngredientPool? _pool;
    private readonly List<IngredientInstance> _candidates = new();

    /// <param name="state">可注入已有 GameState（测试 / 存档恢复用）；为空则新建。</param>
    /// <param name="appearance">食客出现机制配置；为空则使用默认配置。</param>
    /// <param name="random">随机源（食客出现、稀有掉落）；为空则使用 <see cref="Random.Shared"/>。</param>
    public GameController(
        GameState? state = null,
        CustomerAppearanceConfig? appearance = null,
        Random? random = null)
    {
        _state = state ?? new GameState();
        _runController = new RunController(_state);
        _effectSystem = new EffectSystem();
        _appearance = appearance ?? new CustomerAppearanceConfig();
        _random = random ?? Random.Shared;
    }

    /// <summary>游戏运行状态根。</summary>
    public GameState State => _state;

    public RunState Run => _state.Run;
    public PotState Pot => _state.Pot;
    public PlayerState Player => _state.Player;

    /// <summary>当前碗的食客实例（null 表示尚未指派或已结算）。只读。</summary>
    public CustomerInstance? CurrentCustomer => _state.Customer.CurrentCustomer;

    /// <summary>当前是否处于最终锅。</summary>
    public bool IsFinalPot => _state.Run.IsFinalPot;

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
    /// 开始一局新游戏：启动 Run；若食材篮为空则填入初始套装；随后启动第 1 锅。
    /// </summary>
    public void StartNewGame()
    {
        _runController.StartRun();

        if (_state.Player.IngredientBasket.Count == 0)
            _state.Player.IngredientBasket.AddRange(IngredientData.CreateInitialBasket());

        StartCurrentPot();
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
        _potController.StartBowl();

        _pool = new IngredientPool(_state.Player.IngredientBasket, _random);
        _candidates.Clear();

        // 最终锅不逐碗指派食客：整锅由 EndCooking 用占位食客一次性结算。
        if (!_state.Run.IsFinalPot)
            AssignCustomerForBowl();

        AdvanceToIngredientSelection(_potController);

        DrawCandidates();
    }

    /// <summary>
    /// 当前锅结束后推进到下一锅（或最终锅），并立即启动该锅。
    /// 要求当前锅已 Ended（由 <see cref="RunController.AdvanceToNextPot"/> 校验）。
    /// </summary>
    public void AdvanceToNextPot()
    {
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
    /// 预测将候选实例投入当前碗后的本碗基础分，不修改任何真实状态。
    /// 可在 IngredientSelection 阶段（悬停候选时）调用。
    /// </summary>
    public IngredientPreview PreviewIngredient(IngredientInstance candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return EnsurePotController().PreviewIngredient(candidate, _effectSystem);
    }

    /// <summary>
    /// 结束煮粥：结算最终锅。只能在最终锅且锅进行中调用。
    /// 食客与奖励规则尚未接入内容层，暂用正式普通食客数据占位（保持现状）。
    /// </summary>
    public void EndCooking()
    {
        _runController.EndCooking(CustomerData.CreateNormalInstance());
        _candidates.Clear();
        _pool = null;
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
            AdvanceToIngredientSelection(potController);
            DrawCandidates();
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
                potController.CalculateScore();
            else if (_state.Pot.CurrentBowlPhase == BowlPhase.Serving)
                ServeCurrentCustomer();

            potController.AdvanceBowlPhase();
        }

        if (_state.Pot.Phase == PotPhase.InProgress)
        {
            potController.StartNextBowl();
            AssignCustomerForBowl();
            AdvanceToIngredientSelection(potController);
            DrawCandidates();
        }
        else
        {
            // 锅结束：本锅池作废，篮不变。
            _pool = null;
        }
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

        IngredientInstance? rewardIngredient = customer.Definition.IsRare
            ? IngredientData.CreateRandomInstance(_random)
            : null;

        CustomerService.EvaluateAndReward(
            _state.Customer,
            _state.Pot,
            _state.Player,
            _appearance.BaseGoldReward,
            rewardIngredient,
            null);
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
