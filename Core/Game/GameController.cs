using SevenSpices.Core.Companions;
using SevenSpices.Core.Content;
using SevenSpices.Core.Customers;
using SevenSpices.Core.Effects;
using SevenSpices.Core.Events;
using SevenSpices.Core.Ingredients;
using SevenSpices.Core.Items;
using SevenSpices.Core.Pot;
using SevenSpices.Core.Run;
using SevenSpices.Core.Scoring;
using SevenSpices.Core.Shop;

namespace SevenSpices.Core.Game;

/// <summary>
/// 一局游戏对表现层的统一入口（Game Layer）。
/// 持有 GameState + RunController + EffectSystem，封装游戏流程阶段的推进与玩家动作。
/// 每锅开始时从「当前食材篮」生成一份本锅可抽池（快照，不修改篮）；
/// 选中的实例从本锅池移出，未选中的回池；锅结束后本锅池作废，篮不变。
/// 纯 C#，不依赖 Godot / 场景节点。
/// <para>
/// 饕餮 Boss 的章节 / 形态 / 阈值 / 台词 / 赏赐数值集中且唯一源自
/// <see cref="BossConfig.Default"/>（与 <c>CustomerData</c> / <c>ItemData</c> 一致），
/// 不通过构造函数注入，避免判定与上报阈值出现双真源。
/// </para>
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
    private readonly ShopConfig _shopConfig;
    private readonly Random _random;
    private readonly CompanionSystem _companions;
    private readonly FlavorConfig _flavorConfig;
    private PotController? _potController;
    private IngredientPool? _pool;
    private readonly List<IngredientInstance> _candidates = new();
    private readonly List<IngredientInstance> _rewardCandidates = new();
    private bool _rewardResolved;
    private readonly List<CompanionDefinition> _companionCandidates = new();
    private bool _companionResolved;
    private readonly List<ShopOffer> _shopOffers = new();
    private bool _shopResolved;

    /// <summary>饕餮 Boss 静态配置的唯一真源（与 CustomerData / ItemData 读取的是同一份）。</summary>
    private static BossConfig Boss => BossConfig.Default;

    /// <param name="state">可注入已有 GameState（测试 / 存档恢复用）；为空则新建。</param>
    /// <param name="appearance">食客出现机制配置；为空则使用默认配置。</param>
    /// <param name="random">随机源（食客出现、稀有掉落、锅结束奖励、商店陈列抽取）；为空则使用 <see cref="Random.Shared"/>。</param>
    /// <param name="potReward">锅结束奖励配置（X 选 1）；为空则使用默认配置。</param>
    /// <param name="shop">商店配置（陈列数量与价格）；为空则使用默认配置。</param>
    /// <param name="flavorConfig">味道系统可调数值配置；为空则使用 <see cref="FlavorConfig.Default"/>，并透传给每一锅的 PotController（预览与真实结算共用同一份）。</param>
    /// <param name="metaState">局外（跨局）保留状态；为空则新建。由外部持有并在新局开始时注入，以实现跨局保留。</param>
    public GameController(
        GameState? state = null,
        CustomerAppearanceConfig? appearance = null,
        Random? random = null,
        PotRewardConfig? potReward = null,
        ShopConfig? shop = null,
        FlavorConfig? flavorConfig = null,
        MetaState? metaState = null)
    {
        _state = state ?? new GameState();
        Meta = metaState ?? new MetaState();
        _flavorConfig = flavorConfig ?? FlavorConfig.Default;
        // 伙伴系统复用 PlayerState.Companions 的同一列表，不另存副本；须在 RunController 之前建立。
        _companions = new CompanionSystem(_state.Player.Companions);
        _runController = new RunController(_state, _companions, _flavorConfig);
        _events = new EventBus();
        _effectSystem = new EffectSystem(_events);
        _appearance = appearance ?? new CustomerAppearanceConfig();
        _potReward = potReward ?? new PotRewardConfig();
        _shopConfig = shop ?? new ShopConfig();
        _random = random ?? Random.Shared;
    }

    /// <summary>游戏运行状态根。</summary>
    public GameState State => _state;

    /// <summary>本局的只读事件总线，供表现层 / 伙伴系统等订阅刷新。</summary>
    public EventBus Events => _events;

    /// <summary>局外（跨局）保留状态：由外部持有并注入，承载 Boss 赏赐「仙丹粉末」。</summary>
    public MetaState Meta { get; }

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
    /// 是否允许推进到下一锅：当前普通锅已 Ended，且锅结束奖励、伙伴候选、商店三道环节均已处理
    /// （已选定/跳过，或本锅本就未产生候选）。三道环节严格串行：奖励 → 伙伴 → 商店。
    /// 最终锅只能通过 <see cref="EndCooking"/> 结束整局，不能推进。
    /// <para>
    /// 防御存档恢复：构造函数声明可注入已有 GameState（存档恢复用），此时奖励态与伙伴态字段
    /// 为默认值。若对应环节未产生候选，仅靠 resolved 标志会在恢复后卡死；
    /// 追加「无候选即放行」可避免这种双 false 软锁。商店同理（无报价即放行）。
    /// </para>
    /// </summary>
    public bool CanAdvanceToNextPot =>
        _state.Pot.Phase == PotPhase.Ended
        && !_state.Run.IsFinalPot
        && !_state.Run.IsFailed
        && (_rewardResolved || _rewardCandidates.Count == 0)
        && (_companionResolved || _companionCandidates.Count == 0)
        && (_shopResolved || _shopOffers.Count == 0);

    /// <summary>普通锅结束时的 X 选 1 奖励候选（未进入奖励态时为空）。只读。</summary>
    public IReadOnlyList<IngredientInstance> RewardCandidates => _rewardCandidates;

    /// <summary>
    /// 是否正在等待玩家选择锅结束奖励：普通锅、锅已 Ended、尚未选定、且有候选。
    /// 锅结束流程严格串行：奖励 → 伙伴 → 商店；奖励是第一步，未选定前不提供伙伴 / 商店。
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

    /// <summary>
    /// 普通锅结束时的伙伴候选：本锅满意的稀有食客中，有 <see cref="CustomerDefinition.CompanionReward"/>
    /// 的去重映射（保持满意顺序）。未进入候选态时为空。只读。
    /// </summary>
    public IReadOnlyList<CompanionDefinition> CompanionCandidates => _companionCandidates;

    /// <summary>
    /// 是否正在等待玩家选择伙伴：普通锅、锅已 Ended、锅结束奖励已处理完（选定/跳过，或本就无候选）、
    /// 尚未处理（选定/跳过）、且有候选。
    /// 锅结束流程严格串行：奖励 → 伙伴 → 商店；奖励未处理完之前不提供伙伴选择。
    /// </summary>
    public bool IsAwaitingCompanionChoice =>
        !_state.Run.IsFinalPot
        && _state.Pot.Phase == PotPhase.Ended
        && (_rewardResolved || _rewardCandidates.Count == 0)
        && !_companionResolved
        && _companionCandidates.Count > 0;

    /// <summary>兼容别名：语义等同 <see cref="IsAwaitingCompanionChoice"/>，供表现层按钮守卫使用。</summary>
    public bool CanChooseCompanion => IsAwaitingCompanionChoice;

    /// <summary>是否允许跳过伙伴选择：等待选择时可跳过（候选仍会保留到 resolved 由流程处理）。</summary>
    public bool CanSkipCompanionChoice => IsAwaitingCompanionChoice;

    /// <summary>普通锅结束时的商店报价（未进入商店态时为空）。只读。</summary>
    public IReadOnlyList<ShopOffer> ShopOffers => _shopOffers;

    /// <summary>
    /// 是否正在营业：普通锅、锅已 Ended、奖励与伙伴候选均已处理（选定/跳过，或本就无候选）、
    /// 尚未结算（未跳过）、且有报价。商店是锅结束流程的最后一步：必须在「X 选 1 奖励 + 伙伴候选」
    /// 之后才开放，避免三道环节同时可用。「无候选即放行」兼容存档恢复。
    /// </summary>
    public bool IsShopOpen =>
        !_state.Run.IsFinalPot
        && _state.Pot.Phase == PotPhase.Ended
        && (_rewardResolved || _rewardCandidates.Count == 0)
        && (_companionResolved || _companionCandidates.Count == 0)
        && !_shopResolved
        && _shopOffers.Count > 0;

    /// <summary>兼容别名：语义等同 <see cref="IsShopOpen"/>，供表现层「跳过商店」按钮守卫使用。</summary>
    public bool CanSkipShop => IsShopOpen;

    /// <summary>
    /// 是否允许购买指定报价：商店正在营业、索引合法、该件未购买、且金币足够。
    /// </summary>
    public bool CanBuy(int offerIndex)
    {
        if (!IsShopOpen)
            return false;

        if (offerIndex < 0 || offerIndex >= _shopOffers.Count)
            return false;

        var offer = _shopOffers[offerIndex];
        return !offer.IsPurchased && _state.Player.Gold >= offer.Price;
    }

    /// <summary>整局是否已完成：最终锅已结束，或本局已失败终止。委托领域层，语义等价。</summary>
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

        double totalBefore = _state.Pot.BaseScore + _state.Pot.FlavorScore;
        var item = potController.UseItem(instanceId);
        potController.ApplyItemEffect(item, _effectSystem);
        _state.Pot.TotalBaseScore += (int)Math.Floor(_state.Pot.BaseScore + _state.Pot.FlavorScore - totalBefore);

        _events.Publish(new ItemUsedEvent(item));
    }

    /// <summary>
    /// 启动当前 Run 状态下的一锅：
    /// 建本锅池（当前食材篮的快照）→ 开始第 1 碗 → 推进到 IngredientSelection → 抽取候选。
    /// </summary>
    public void StartCurrentPot()
    {
        // 防御：本局已终止（章末被嫌弃）时不允许再启动新锅，与 RunController 的失败收口一致。
        // StartNewGame 会先经 StartRun 清空失败态，因此正常新局不受影响。
        if (_state.Run.IsFailed)
            throw new InvalidOperationException(
                "Cannot start pot: the run has failed. Fresh pot is not allowed after a failed run.");

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

        // 伙伴候选状态同样按「本锅」重置。
        _companionCandidates.Clear();
        _companionResolved = false;

        // 商店状态同样按「本锅」重置。
        _shopOffers.Clear();
        _shopResolved = false;

        // 最终锅不逐碗指派食客：整锅由 EndCooking 用饕餮真身一次性结算。
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
        // 防御：本局已终止时不允许推进，与 RunController.AdvanceToNextPot 的失败守卫一致。
        if (_state.Run.IsFailed)
            throw new InvalidOperationException(
                "Cannot advance: the run has failed. Fresh pot is not allowed after a failed run.");

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
    /// 选定一位伙伴：加入 <see cref="PlayerState.Companions"/>（CompanionSystem 复用同一列表，
    /// 立即对后续食材 / 锅底生效），清空候选并标记已处理，随后才允许推进下一锅。
    /// 发布 <see cref="CompanionAddedEvent"/>。只能在 <see cref="CanChooseCompanion"/> 为真时调用。
    /// </summary>
    public void ChooseCompanion(string companionId)
    {
        if (!CanChooseCompanion)
            throw new InvalidOperationException(
                $"Cannot choose companion: pot={_state.Pot.Phase}, final={_state.Run.IsFinalPot}, resolved={_companionResolved}, candidates={_companionCandidates.Count}.");

        var definition = _companionCandidates.FirstOrDefault(c => c.Id == companionId)
            ?? throw new ArgumentException($"Companion candidate '{companionId}' not found.", nameof(companionId));

        var instance = new CompanionInstance(definition);
        _state.Player.Companions.Add(instance);
        _companionCandidates.Clear();
        _companionResolved = true;

        _events.Publish(new CompanionAddedEvent(instance));
    }

    /// <summary>
    /// 跳过伙伴选择：清空候选并标记已处理，随后允许推进下一锅，不获得任何伙伴。
    /// 只能在 <see cref="CanSkipCompanionChoice"/> 为真时调用。
    /// </summary>
    public void SkipCompanionChoice()
    {
        if (!CanSkipCompanionChoice)
            throw new InvalidOperationException(
                $"Cannot skip companion choice: pot={_state.Pot.Phase}, final={_state.Run.IsFinalPot}, resolved={_companionResolved}, candidates={_companionCandidates.Count}.");

        int candidateCount = _companionCandidates.Count;
        _companionCandidates.Clear();
        _companionResolved = true;

        _events.Publish(new CompanionChoiceSkippedEvent(candidateCount));
    }

    /// <summary>
    /// 购买指定报价：扣除金币、把食材加入食材篮 / 道具加入道具栏、标记该件已购买，
    /// 发布 <see cref="ShopPurchasedEvent"/>。只能在 <see cref="CanBuy"/> 为真时调用。
    /// </summary>
    public void Buy(int offerIndex)
    {
        // 失败原因分开抛出，便于排查（商店未开放 / 索引越界 / 已购买 / 金币不足）。
        if (!IsShopOpen)
            throw new InvalidOperationException(
                $"Cannot buy offer #{offerIndex}: shop is not open " +
                $"(pot={_state.Pot.Phase}, final={_state.Run.IsFinalPot}, resolved={_shopResolved}, offers={_shopOffers.Count}).");

        if (offerIndex < 0 || offerIndex >= _shopOffers.Count)
            throw new ArgumentOutOfRangeException(
                nameof(offerIndex), offerIndex, $"Offer index out of range (offers={_shopOffers.Count}).");

        var offer = _shopOffers[offerIndex];

        if (offer.IsPurchased)
            throw new InvalidOperationException(
                $"Cannot buy offer #{offerIndex}: already purchased.");

        if (_state.Player.Gold < offer.Price)
            throw new InvalidOperationException(
                $"Cannot buy offer #{offerIndex}: insufficient gold (gold={_state.Player.Gold}, price={offer.Price}).");

        _state.Player.Gold -= offer.Price;

        if (offer.Kind == ShopOfferKind.Ingredient)
            _state.Player.IngredientBasket.Add(offer.Ingredient!);
        else
            _state.Player.Items.Add(offer.Item!);

        offer.MarkPurchased();
        _events.Publish(new ShopPurchasedEvent(offer));
    }

    /// <summary>
    /// 跳过商店（或不再购买）：标记商店已结算并清空报价，随后允许推进下一锅。
    /// 只能在 <see cref="CanSkipShop"/> 为真时调用。
    /// </summary>
    public void SkipShop()
    {
        if (!CanSkipShop)
            throw new InvalidOperationException(
                $"Cannot skip shop: pot={_state.Pot.Phase}, final={_state.Run.IsFinalPot}, resolved={_shopResolved}, offers={_shopOffers.Count}.");

        int offerCount = _shopOffers.Count;
        _shopResolved = true;
        _shopOffers.Clear();

        _events.Publish(new ShopSkippedEvent(offerCount));
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
    /// 由饕餮真身试吃，判定结果落库为本局结局（满意＝尚可 / 嫌弃＝重来）；
    /// 无论哪种，本局都在此结束（轮回），不按失败收口（<see cref="RunState.IsFailed"/> 保持 false）。
    /// </summary>
    public void EndCooking()
    {
        // 入口守卫：必须在任何副作用之前校验（与本类其它动作方法一致），
        // 否则非最终锅 / 分数已锁时误调会先兑现陈酿池并发出假的真身遭遇事件。
        if (!CanEndCooking)
            throw new InvalidOperationException(
                $"Cannot end cooking: final={_state.Run.IsFinalPot}, pot={_state.Pot.Phase}.");

        // 架构 §32.6 / 设计文档 §24.2：最终锅不逐碗，陈酿池在结算前立即全额兑现，
        // 使其参与随后的分数计算与食客判定。
        EnsurePotController().RealizeAgingPool();

        // 最终锅由饕餮真身试吃：替换普通食客占位，并上报「遭遇」事件。
        var form = BossConfig.Default.TrueForm;
        var customer = CustomerData.CreateTaotieInstance(form.Id);
        // 最终锅整锅作为「一大碗」，BowlNumber 恒为 ScoreCalculator.FinalPotBowlNumber（10）；
        // 此处并非「第 10 碗」语义，表现层不要据此当成逐碗流程。
        _events.Publish(new BossEncounteredEvent(
            form.Id, form.Name, _state.Run.Chapter, _state.Run.PotIndex,
            _state.Run.IsFinalPot, _state.Pot.BowlNumber));

        // 取用判定返回值（方案 §4.4）：真身判定仍复用 EvaluateAndReward。
        bool satisfied = _runController.EndCooking(customer);

        // 结局落库：满意 = 尚可，嫌弃 = 重来；无论哪种，本局都在此结束（轮回）。
        _state.Run.Outcome = satisfied ? RunOutcome.Okay : RunOutcome.Restart;

        bool rewardGranted = false;
        if (satisfied)
            rewardGranted = Meta.TryAddImmortalPowder(ItemData.CreateSpecialInstance(form.RewardItemId));

        _state.Run.ChapterBossRecords.Add(new ChapterBossRecord(
            _state.Run.Chapter, _state.Run.PotIndex, form.Id, form.Name,
            satisfied, _state.Pot.TotalFinalScore, form.SatisfyThreshold, isFinalPot: true));

        _candidates.Clear();
        _pool = null;
        _rewardCandidates.Clear();
        _rewardResolved = false;
        _companionCandidates.Clear();
        _companionResolved = false;
        _shopOffers.Clear();
        _shopResolved = false;

        // 最终锅由 RunController 内部一次性结算；此处只读取已结算的状态发布事件。
        var pot = _state.Pot;
        int multiplier = ScoreCalculator.GetEffectiveMultiplier(pot);
        _events.Publish(new ScoreCalculatedEvent((int)Math.Floor(pot.BaseScoreWithFlavor), pot.FinalScore, multiplier));
        _events.Publish(new ScoreLockedEvent(pot.FinalScore));
        _events.Publish(new CustomerServedEvent(customer, 0));
        _events.Publish(new PotEndedEvent(
            _state.Run.Chapter, _state.Run.PotIndex, _state.Run.IsFinalPot));

        if (satisfied)
            _events.Publish(new CustomerSatisfiedEvent(customer));

        _events.Publish(new BossEvaluatedEvent(
            form.Id, form.Name, _state.Run.Chapter, _state.Run.PotIndex, _state.Run.IsFinalPot,
            satisfied, form.SatisfyThreshold, pot.TotalFinalScore, rewardGranted));

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

            // 本局已终止（章末被饕餮嫌弃吞下）：收口不再产出，避免失败后仍停在候选态。
            // 清空并置位三道环节（奖励 / 伙伴 / 商店），随后直接返回，不发布任何 Offered 事件。
            if (_state.Run.IsFailed)
            {
                _rewardCandidates.Clear();
                _rewardResolved = true;
                _companionCandidates.Clear();
                _companionResolved = true;
                _shopOffers.Clear();
                _shopResolved = true;
                return;
            }

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

            // 伙伴候选（设计文档 §3.3 / §21）：本锅满意的稀有食客中，有 CompanionReward 的
            // 映射为伙伴定义，按满意顺序去重。此分支只在普通锅走到（最终锅不经 FinishBowl），
            // 天然不触发最终锅候选。
            _companionCandidates.Clear();
            foreach (var customer in _state.Customer.SatisfiedRareCustomers)
            {
                var companionReward = customer.Definition.CompanionReward;
                if (companionReward == null || _companionCandidates.Contains(companionReward))
                    continue;
                _companionCandidates.Add(companionReward);
            }

            // 无候选时直接视为已处理，避免卡住推进（与奖励兜底同理）。
            if (_companionCandidates.Count == 0)
                _companionResolved = true;

            // 商店（设计文档 §18）：普通锅结束后陈列随机食材 + 道具，各自互不重复，可跳过。
            // 此分支只在普通锅走到（最终锅不经 FinishBowl），天然不触发最终锅商店。
            _shopOffers.Clear();
            foreach (var ingredient in
                     IngredientData.CreateRandomInstances(_shopConfig.IngredientOfferCount, _random))
            {
                _shopOffers.Add(new ShopOffer(ingredient, _shopConfig.IngredientPrice));
            }

            foreach (var item in
                     ItemData.CreateRandomInstances(_shopConfig.ItemOfferCount, _random))
            {
                _shopOffers.Add(new ShopOffer(item, _shopConfig.ItemPrice));
            }

            // 无报价时直接视为已结算，避免卡住推进（与奖励/伙伴兜底同理）。
            if (_shopOffers.Count == 0)
                _shopResolved = true;

            _events.Publish(new RewardOfferedEvent(_rewardCandidates));

            // 未生成商店（0 报价）时不发布 ShopOfferedEvent：无商店就没有「已开放」的语义。
            if (_shopOffers.Count > 0)
                _events.Publish(new ShopOfferedEvent(_shopOffers));
        }
    }

    /// <summary>在分数计算并锁定后发布 ScoreCalculatedEvent + ScoreLockedEvent（顺序固定）。</summary>
    private void PublishScoreEvents()
    {
        var pot = _state.Pot;
        int multiplier = ScoreCalculator.GetEffectiveMultiplier(pot);
        _events.Publish(new ScoreCalculatedEvent((int)Math.Floor(pot.BaseScoreWithFlavor), pot.FinalScore, multiplier));
        _events.Publish(new ScoreLockedEvent(pot.FinalScore));
    }

    /// <summary>
    /// 按当前碗号为该碗指派食客：先判章末 boss，否则维持普通/稀有逻辑。
    /// 只在普通锅的每碗开始时调用。
    /// </summary>
    private void AssignCustomerForBowl()
        => CustomerService.AssignCustomer(_state.Customer, ResolveCustomerForBowl());

    /// <summary>
    /// 章末 Boss 锅：非最终锅 + 本锅是本章最后一锅 + 当前碗 == 本锅最后一碗，
    /// 由本章对应形态的饕餮顶替普通食客；否则维持既有普通/稀有逻辑（第 5/7 碗稀有行为不变）。
    /// </summary>
    private CustomerInstance ResolveCustomerForBowl()
    {
        var form = GetChapterBossFormForCurrentBowl();
        if (form == null)
            return _appearance.CreateCustomerForBowl(_state.Pot.BowlNumber, _random);

        var customer = CustomerData.CreateTaotieInstance(form.Id);
        _events.Publish(new BossEncounteredEvent(
            form.Id, form.Name, _state.Run.Chapter, _state.Run.PotIndex,
            _state.Run.IsFinalPot, _state.Pot.BowlNumber));
        return customer;
    }

    /// <summary>
    /// 当前碗是否应指派章末饕餮；命中则返回本章对应形态，否则返回 null。
    /// 判定全部取自 <see cref="RunController.PotsPerChapter"/> 与 <see cref="BossConfig.Default"/>，不硬编码。
    /// </summary>
    private BossFormConfig? GetChapterBossFormForCurrentBowl()
    {
        var run = _state.Run;
        if (run.IsFinalPot || run.IsFailed)
            return null;
        if (run.PotIndex != RunController.PotsPerChapter)
            return null;
        // 章末试吃＝本章最后一锅的最后一碗（BowlLimit 即本锅碗数：普通锅 10，最终锅 int.MaxValue 但上面已排除）。
        if (_state.Pot.BowlNumber != _state.Pot.BowlLimit)
            return null;
        return Boss.GetChapterForm(run.Chapter);
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

        // 章末 Boss（饕餮）走专属结算：不走随机掉落、不发金币。
        var bossForm = Boss.FindForm(customer.Definition.Id);
        if (bossForm != null)
        {
            ServeBoss(customer, bossForm);
            return;
        }

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

    /// <summary>
    /// 饕餮试吃结算：满意 → 赏赐「仙丹粉末」并存入跨局容器（至多 1 个）；嫌弃 → 本局终止。
    /// 不走随机掉落（方案 §4.3），也不发金币（饕餮不付钱）。
    /// </summary>
    private void ServeBoss(CustomerInstance customer, BossFormConfig form)
    {
        var pot = _state.Pot;
        var run = _state.Run;

        // 复用既有评价：饕餮的满意条件为 PotTotalScoreAtLeast，读的是本锅累计最终分。
        bool satisfied = CustomerService.EvaluateAndReward(
            _state.Customer, pot, _state.Player, baseGoldReward: 0);

        run.ChapterBossRecords.Add(new ChapterBossRecord(
            run.Chapter, run.PotIndex, form.Id, form.Name,
            satisfied, pot.TotalFinalScore, form.SatisfyThreshold, isFinalPot: false));

        _events.Publish(new CustomerServedEvent(customer, 0));

        bool rewardGranted = false;
        if (satisfied)
        {
            // Boss 专属产出：按 BossConfig 的 RewardItemId 取道具，进跨局容器 MetaState
            //（不进随机掉落 / 商店 / 伙伴）。
            rewardGranted = Meta.TryAddImmortalPowder(ItemData.CreateSpecialInstance(form.RewardItemId));
            _events.Publish(new CustomerSatisfiedEvent(customer));
        }
        else
        {
            _runController.FailRun(Boss.LoseLine);
        }

        _events.Publish(new BossEvaluatedEvent(
            form.Id, form.Name, run.Chapter, run.PotIndex, run.IsFinalPot,
            satisfied, form.SatisfyThreshold, pot.TotalFinalScore, rewardGranted));

        if (!satisfied)
            _events.Publish(new RunFailedEvent(Boss.LoseLine, run.Chapter, run.PotIndex));
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
        => _potController ??= new PotController(_state, _companions, flavorConfig: _flavorConfig);
}
