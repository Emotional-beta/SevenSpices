using SevenSpices.Core.Content;
using SevenSpices.Core.Events;
using SevenSpices.Core.Game;
using SevenSpices.Core.Items;
using SevenSpices.Core.Pot;
using SevenSpices.Core.Run;
using SevenSpices.Core.Scoring;

namespace SevenSpices.Tests.Game;

/// <summary>
/// GameController 事件发布测试：固定种子，断言关键事件在正确节点按正确顺序触发，
/// 并验证事件发布不改变游戏状态。
/// </summary>
public static class GameControllerEventTests
{
    public static void RunAll()
    {
        Test_StartNewGame_PublishesPotBowlDrawnInOrder();
        Test_SelectIngredient_PublishesIngredientAddedFirst();
        Test_BowlSettlement_ScoreBeforeLockBeforeServed();
        Test_PotEnd_PublishesPotEndedAndRewardOffered_ThenChooseReward();
        Test_UseItem_PublishesItemUsedAndEffectTriggered();
        Test_RareCustomerSatisfied_PublishesSatisfiedEvent();
        Test_FinalPot_EndCooking_PublishesRunCompletedLast();
        Test_EventPayloads_MatchResultingState();
        Test_NoSubscribers_DoNotChangeGameState();

        Console.WriteLine("All GameControllerEventTests passed.");
    }

    // ── 辅助 ─────────────────────────────────────────────────────────────────

    private sealed class EventLog
    {
        public List<GameEvent> Events { get; } = new();
        public void Handle(GameEvent gameEvent) => Events.Add(gameEvent);
        public bool Has<T>() where T : GameEvent => Events.OfType<T>().Any();
        public IEnumerable<T> Of<T>() where T : GameEvent => Events.OfType<T>();
        public int IndexOf<T>() where T : GameEvent => Events.FindIndex(e => e is T);
    }

    /// <summary>关闭稀有食客，避免随机掉落干扰。</summary>
    static CustomerAppearanceConfig NoRare() =>
        new() { RareBowlNumbers = Array.Empty<int>() };

    /// <summary>用「能选就选、池空就跳」走完当前普通锅到 Ended，但不解决奖励。</summary>
    static void FinishCurrentPot(GameController gc)
    {
        int guard = 0;
        while (gc.Pot.Phase == PotPhase.InProgress && guard++ < 500)
        {
            if (gc.CanSelectIngredient)
                gc.SelectIngredient(gc.CurrentCandidates[0].InstanceId);
            else if (gc.CanSkipBowl)
                gc.SkipBowl();
            else
                break;
        }
        Assert(gc.Pot.Phase == PotPhase.Ended, "FinishCurrentPot: 普通锅应已 Ended");
    }

    /// <summary>直接经 RunController 走完 9 锅普通锅，进入最终锅。</summary>
    static void AdvanceToFinalPot(RunController run, GameState state)
    {
        int total = RunController.ChaptersPerRun * RunController.PotsPerChapter;
        for (int i = 0; i < total; i++)
        {
            var ctrl = run.StartCurrentPot();
            for (int bowl = 1; bowl <= 10; bowl++)
            {
                ctrl.StartBowl();
                for (int s = 0; s < 9; s++) ctrl.AdvanceBowlPhase();
                if (bowl < 10) ctrl.StartNextBowl();
            }
            ctrl.ClosePot();
            run.AdvanceToNextPot();
        }
        Assert(state.Run.IsFinalPot, "AdvanceToFinalPot: 应已进入最终锅");
    }

    /// <summary>在固定种子下走完一锅（含奖励选择），返回用于对比的状态快照。</summary>
    static (int TotalBase, int FinalScore, int Gold, int Bowl, int BottomSweet, int Basket)
        RunFixedScenario(bool subscribe)
    {
        var gc = new GameController(new GameState(), NoRare(), new Random(4321));
        if (subscribe)
            gc.Events.Subscribe(_ => { });

        gc.StartNewGame();
        FinishCurrentPot(gc);
        if (gc.IsAwaitingReward)
            gc.ChooseReward(gc.RewardCandidates[0].InstanceId);

        return (gc.Pot.TotalBaseScore, gc.Pot.FinalScore, gc.Player.Gold, gc.Pot.BowlNumber,
            gc.State.Bottom.GetFlavor(FlavorType.Sweet), gc.Player.IngredientBasket.Count);
    }

    // ── 测试 ─────────────────────────────────────────────────────────────────

    /// <summary>StartNewGame → ProfessionChosen → PotStarted → BowlStarted → IngredientDrawn，且只发布这四个。</summary>
    static void Test_StartNewGame_PublishesPotBowlDrawnInOrder()
    {
        var gc = new GameController(new GameState(), NoRare(), new Random(3001));
        var log = new EventLog();
        gc.Events.Subscribe(log.Handle);

        gc.StartNewGame();

        Assert(log.Events.Count == 4, $"StartNewGame 应恰好发布 4 个事件，实际 {log.Events.Count}");
        Assert(log.Events[0] is ProfessionChosenEvent, "第 1 个事件应为 ProfessionChosenEvent");
        Assert(log.Events[1] is PotStartedEvent, "第 2 个事件应为 PotStartedEvent");
        Assert(log.Events[2] is BowlStartedEvent, "第 3 个事件应为 BowlStartedEvent");
        Assert(log.Events[3] is IngredientDrawnEvent, "第 4 个事件应为 IngredientDrawnEvent");

        var pot = (PotStartedEvent)log.Events[1];
        Assert(pot.Chapter == 1 && pot.PotIndex == 1 && !pot.IsFinalPot,
            "PotStartedEvent 应携带第 1 章第 1 锅且非最终锅");

        var bowl = (BowlStartedEvent)log.Events[2];
        Assert(bowl.BowlNumber == 1, "BowlStartedEvent 应携带第 1 碗");

        var drawn = (IngredientDrawnEvent)log.Events[3];
        Assert(drawn.Candidates.Count == 3, "IngredientDrawnEvent 应携带 3 个候选");
    }

    /// <summary>SelectIngredient → IngredientAddedEvent 在收尾事件之前发布，且携带所选实例。</summary>
    static void Test_SelectIngredient_PublishesIngredientAddedFirst()
    {
        var gc = new GameController(new GameState(), NoRare(), new Random(3002));
        gc.StartNewGame();

        var log = new EventLog();
        gc.Events.Subscribe(log.Handle);

        var chosen = gc.CurrentCandidates[0];
        gc.SelectIngredient(chosen.InstanceId);

        Assert(log.Events.Count > 0 && log.Events[0] is IngredientAddedEvent,
            "SelectIngredient 首个事件应为 IngredientAddedEvent");

        var added = (IngredientAddedEvent)log.Events[0];
        Assert(ReferenceEquals(added.Ingredient, chosen), "IngredientAddedEvent 应携带所选实例");
    }

    /// <summary>走完一碗：ScoreCalculated 早于 ScoreLocked，CustomerServed 在其后。</summary>
    static void Test_BowlSettlement_ScoreBeforeLockBeforeServed()
    {
        var appearance = NoRare();
        var gc = new GameController(new GameState(), appearance, new Random(3003));
        gc.StartNewGame();

        var log = new EventLog();
        gc.Events.Subscribe(log.Handle);

        // 选非辣食材（米饭）：辣·余温会临时提高碗数倍率，本测试验证的是无余温时的 ×1。
        var chosen = gc.CurrentCandidates.First(c => c.Definition.Id == "rice");
        gc.SelectIngredient(chosen.InstanceId);

        int calc = log.IndexOf<ScoreCalculatedEvent>();
        int locked = log.IndexOf<ScoreLockedEvent>();
        int served = log.IndexOf<CustomerServedEvent>();

        Assert(calc >= 0 && locked >= 0 && served >= 0,
            "本碗结算应发布 ScoreCalculated、ScoreLocked、CustomerServed");
        Assert(calc < locked, "ScoreCalculatedEvent 应早于 ScoreLockedEvent");
        Assert(locked < served, "ScoreLockedEvent 应早于 CustomerServedEvent");

        var calcEvent = log.Of<ScoreCalculatedEvent>().First();
        var lockedEvent = log.Of<ScoreLockedEvent>().First();
        Assert(calcEvent.FinalScore == lockedEvent.FinalScore,
            "ScoreCalculated 与 ScoreLocked 的 FinalScore 应一致");
        Assert(calcEvent.Multiplier == ScoreCalculator.GetEffectiveMultiplier(gc.Pot),
            "第 1 碗倍率应为实际生效倍率（含辣·余温口径）×1");

        var servedEvent = log.Of<CustomerServedEvent>().First();
        Assert(servedEvent.GoldAwarded == appearance.BaseGoldReward,
            "普通食客播报的金币应等于配置的 BaseGoldReward");
    }

    /// <summary>锅结束 → PotEnded 早于 RewardOffered；ChooseReward → RewardChosen。</summary>
    static void Test_PotEnd_PublishesPotEndedAndRewardOffered_ThenChooseReward()
    {
        var gc = new GameController(new GameState(), NoRare(), new Random(3004));
        gc.StartNewGame();

        var log = new EventLog();
        gc.Events.Subscribe(log.Handle);

        FinishCurrentPot(gc);

        Assert(log.Has<PotEndedEvent>(), "锅结束应发布 PotEndedEvent");
        Assert(log.Of<RewardOfferedEvent>().Count() == 1, "普通锅结束应发布一次 RewardOfferedEvent");
        Assert(log.IndexOf<PotEndedEvent>() < log.IndexOf<RewardOfferedEvent>(),
            "PotEndedEvent 应早于 RewardOfferedEvent");
        Assert(gc.IsAwaitingReward, "应处于等待选择奖励状态");

        var reward = gc.RewardCandidates[0];
        log.Events.Clear();
        gc.ChooseReward(reward.InstanceId);

        var chosen = log.Of<RewardChosenEvent>().SingleOrDefault();
        Assert(chosen != null, "ChooseReward 应发布 RewardChosenEvent");
        Assert(ReferenceEquals(chosen!.Ingredient, reward), "RewardChosenEvent 应携带所选实例");
    }

    /// <summary>UseItem → ItemUsedEvent；注入 EventBus 后效果执行也发布 EffectTriggeredEvent。</summary>
    static void Test_UseItem_PublishesItemUsedAndEffectTriggered()
    {
        var gc = new GameController(new GameState(), NoRare(), new Random(3005));
        gc.StartNewGame();

        gc.Player.Items.Clear();
        var salt = ItemData.CreateInstance("salt");
        gc.Player.Items.Add(salt);

        var log = new EventLog();
        gc.Events.Subscribe(log.Handle);

        gc.UseItem(salt.InstanceId);

        Assert(log.Has<EffectTriggeredEvent>(), "效果执行应发布 EffectTriggeredEvent");
        var used = log.Of<ItemUsedEvent>().SingleOrDefault();
        Assert(used != null, "UseItem 应发布 ItemUsedEvent");
        Assert(ReferenceEquals(used!.Item, salt), "ItemUsedEvent 应携带所用实例");
    }

    /// <summary>稀有食客满意：CustomerServed 之后补发 CustomerSatisfied。</summary>
    static void Test_RareCustomerSatisfied_PublishesSatisfiedEvent()
    {
        var appearance = new CustomerAppearanceConfig
        {
            RareBowlNumbers = new[] { 1 },
            RareProbability = 1.0,
            // 显式使用通用稀有食客（分数 ≥20 或甜味 ≥5），本测试用 7 个食盐垫高分数触发满意。
            RareCustomers = new[] { CustomerData.RareCustomer },
        };
        var gc = new GameController(new GameState(), appearance, new Random(3007));
        gc.StartNewGame();

        Assert(gc.CurrentCustomer != null && gc.CurrentCustomer.Definition.IsRare,
            "第 1 碗应为稀有食客");

        gc.Player.Items.Clear();
        for (int i = 0; i < 7; i++)
            gc.Player.Items.Add(ItemData.CreateInstance("salt"));
        while (gc.CanUseItem)
            gc.UseItem(gc.Items[0].InstanceId);

        var log = new EventLog();
        gc.Events.Subscribe(log.Handle);
        gc.SelectIngredient(gc.CurrentCandidates[0].InstanceId);

        Assert(log.Of<CustomerServedEvent>().Count() == 1, "应发布一次 CustomerServedEvent");
        Assert(log.Of<CustomerSatisfiedEvent>().Count() == 1, "满意稀有食客应发布 CustomerSatisfiedEvent");
        Assert(log.IndexOf<CustomerServedEvent>() < log.IndexOf<CustomerSatisfiedEvent>(),
            "CustomerSatisfiedEvent 应在 CustomerServedEvent 之后");

        var served = log.Of<CustomerServedEvent>().First();
        Assert(served.GoldAwarded == 0, "稀有食客不发基础金币");
    }

    /// <summary>完整一局 EndCooking → 最终分锁定 + 食客 + 锅结束 + RunCompleted（最后）。</summary>
    static void Test_FinalPot_EndCooking_PublishesRunCompletedLast()
    {
        var state = new GameState();
        var run = new RunController(state);
        run.StartRun();
        AdvanceToFinalPot(run, state);

        var gc = new GameController(state);
        gc.StartCurrentPot();

        var log = new EventLog();
        gc.Events.Subscribe(log.Handle);

        gc.EndCooking();

        Assert(log.Has<ScoreCalculatedEvent>(), "最终锅结算应发布 ScoreCalculatedEvent");
        Assert(log.Has<ScoreLockedEvent>(), "最终锅结算应发布 ScoreLockedEvent");
        Assert(log.Has<CustomerServedEvent>(), "最终锅结算应发布 CustomerServedEvent");
        Assert(log.Has<PotEndedEvent>(), "最终锅结算应发布 PotEndedEvent");
        Assert(log.Of<RunCompletedEvent>().Count() == 1, "最终锅结算应发布一次 RunCompletedEvent");
        Assert(log.Events[^1] is RunCompletedEvent, "RunCompletedEvent 应为结算的最后一个事件");
        Assert(gc.IsRunComplete, "EndCooking 后本局应完成");
    }

    /// <summary>
    /// 事件负载必须与派发时的真实状态一致（可证伪）：
    /// 投料事件携带的实例应真实存在于 Pot.Ingredients；
    /// ScoreCalculated / ScoreLocked 携带的分数应等于派发时 Pot.FinalScore；
    /// CustomerServed 携带的金币应等于玩家实际金币增量。
    /// </summary>
    static void Test_EventPayloads_MatchResultingState()
    {
        var gc = new GameController(new GameState(), NoRare(), new Random(4321));

        var log = new EventLog();
        int scoreAtCalc = -1, scoreAtLock = -1, baseAtCalc = -1;
        gc.Events.Subscribe(gameEvent =>
        {
            log.Events.Add(gameEvent);
            switch (gameEvent)
            {
                case ScoreCalculatedEvent:
                    scoreAtCalc = gc.Pot.FinalScore;
                    baseAtCalc = (int)Math.Floor(gc.Pot.BaseScoreWithFlavor);
                    break;
                case ScoreLockedEvent:
                    scoreAtLock = gc.Pot.FinalScore;
                    break;
            }
        });

        gc.StartNewGame();

        int goldBefore = gc.Player.Gold;
        var chosen = gc.CurrentCandidates[0];
        gc.SelectIngredient(chosen.InstanceId);

        var added = log.Of<IngredientAddedEvent>().Single();
        Assert(ReferenceEquals(added.Ingredient, chosen), "IngredientAddedEvent 应携带所选实例");
        Assert(gc.Pot.Ingredients.Any(i => ReferenceEquals(i, added.Ingredient)),
            "IngredientAddedEvent 携带的实例应真实存在于 Pot.Ingredients");

        var calcEvent = log.Of<ScoreCalculatedEvent>().Single();
        var lockedEvent = log.Of<ScoreLockedEvent>().Single();
        Assert(calcEvent.FinalScore == scoreAtCalc,
            $"ScoreCalculatedEvent.FinalScore({calcEvent.FinalScore}) 应等于派发时 Pot.FinalScore({scoreAtCalc})");
        Assert(calcEvent.BaseScore == baseAtCalc,
            $"ScoreCalculatedEvent.BaseScore({calcEvent.BaseScore}) 应等于派发时含味道分基础分({baseAtCalc})，口径不得漏掉味道分");
        Assert(lockedEvent.FinalScore == scoreAtLock,
            $"ScoreLockedEvent.FinalScore({lockedEvent.FinalScore}) 应等于派发时 Pot.FinalScore({scoreAtLock})");
        Assert(calcEvent.FinalScore == lockedEvent.FinalScore,
            "ScoreCalculated 与 ScoreLocked 的 FinalScore 应一致");

        var servedEvent = log.Of<CustomerServedEvent>().Single();
        Assert(servedEvent.GoldAwarded == gc.Player.Gold - goldBefore,
            $"CustomerServedEvent.GoldAwarded({servedEvent.GoldAwarded}) 应等于玩家金币增量({gc.Player.Gold - goldBefore})");
    }

    /// <summary>
    /// 辅助断言：空订阅者不改变状态 —— 同一固定种子下，0 订阅者与 N 订阅者跑完同一场景的最终状态一致。
    /// 注意：这只能说明「空订阅者不改变状态」，并不能证明「发布是纯附加动作」。
    /// </summary>
    static void Test_NoSubscribers_DoNotChangeGameState()
    {
        var withoutSubscriber = RunFixedScenario(subscribe: false);
        var withSubscriber = RunFixedScenario(subscribe: true);

        Assert(withoutSubscriber == withSubscriber,
            $"空订阅者不应改变游戏状态：无订阅={withoutSubscriber}，有订阅={withSubscriber}");
    }

    static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception($"[FAIL] GameControllerEventTests: {message}");
    }
}
