using SevenSpices.Core.Content;
using SevenSpices.Core.Customers;
using SevenSpices.Core.Events;
using SevenSpices.Core.Game;
using SevenSpices.Core.Ingredients;
using SevenSpices.Core.Items;
using SevenSpices.Core.Pot;
using SevenSpices.Core.Run;
using SevenSpices.Core.Shop;

namespace SevenSpices.Tests.Run;

/// <summary>
/// 路线系统（餐饮风潮）测试（方案 §六 验收）+ R0 加权抽取零回归。
/// 覆盖：时点与门控、候选构成、跳过吃保底、风潮倾斜与到期、3/6/9 各触发一次、
/// 固定种子可复现，以及默认 null 权重下行为与旧实现逐位一致。
/// </summary>
public static class RouteSystemTests
{
    public static void RunAll()
    {
        Test_ChapterEnd_OffersRouteOnlyAfterShopSettled();
        Test_RouteCandidates_OneFallbackTwoTrends_Distinct();
        Test_ActiveTrend_BiasesNextPotReward();
        Test_NewTrendRoutes_BiasRewardPool();
        Test_ActiveTrend_ExpiresAfterItsChapter();
        Test_SkipRoute_GrantsFallbackReward();
        Test_RouteOfferedAtPots3_6_9();
        Test_RouteCandidates_ReproducibleWithFixedSeed();
        Test_R0_NullWeight_MatchesLegacyBehavior();
        Test_R0_WeightedDraw_CanBias();

        Console.WriteLine("All RouteSystemTests passed.");
    }

    // ── 辅助 ─────────────────────────────────────────────────────────────────

    static CustomerAppearanceConfig NoRare() =>
        new() { RareBowlNumbers = Array.Empty<int>() };

    /// <summary>关闭商店（陈列数量为 0），用于聚焦奖励 / 风潮抽取的场景。</summary>
    static ShopConfig NoShop() =>
        new() { IngredientOfferCount = 0, ItemOfferCount = 0 };

    static GameController NewController(
        int seed,
        CustomerAppearanceConfig? appearance = null,
        PotRewardConfig? potReward = null,
        ShopConfig? shop = null) =>
        new(new GameState(), appearance ?? NoRare(), new Random(seed), potReward, shop);

    /// <summary>
    /// 用「能选就选、池空就跳」走完当前锅到 Ended（不处理收尾环节）。
    /// 章末锅（每章第 3 锅）在最后一碗注入高基础分，保证章末饕餮试吃满意，
    /// 否则本局会被嫌弃终止、路线不会触发。
    /// </summary>
    static void FinishPot(GameController gc)
    {
        int guard = 0;
        while (gc.Pot.Phase == PotPhase.InProgress && guard++ < 500)
        {
            if (gc.Run.PotIndex == RunController.PotsPerChapter
                && gc.Pot.BowlNumber == gc.Pot.BowlLimit)
            {
                gc.Pot.BaseScore = 1000;
            }

            if (gc.CanSelectIngredient)
                gc.SelectIngredient(gc.CurrentCandidates[0].InstanceId);
            else if (gc.CanSkipBowl)
                gc.SkipBowl();
            else
                break;
        }

        Assert(gc.Pot.Phase == PotPhase.Ended, "FinishPot: 锅应已 Ended");
    }

    /// <summary>结算锅收尾的奖励 → 伙伴 → 商店 → 路线（若存在），用于连续推进多锅。</summary>
    static void SettlePot(GameController gc)
    {
        if (gc.IsAwaitingReward)
            gc.ChooseReward(gc.RewardCandidates[0].InstanceId);
        if (gc.IsAwaitingCompanionChoice)
            gc.SkipCompanionChoice();
        if (gc.IsShopOpen)
            gc.SkipShop();
        if (gc.IsAwaitingRouteChoice)
            gc.SkipRoute();
    }

    static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception($"[FAIL] RouteSystemTests: {message}");
    }

    // ── 1. 时点与门控：第 3 锅商店结算后才提供路线 ───────────────────────────

    static void Test_ChapterEnd_OffersRouteOnlyAfterShopSettled()
    {
        var gc = NewController(seed: 8001);
        gc.StartNewGame();

        // 第 1、2 锅非章末，不应提供路线，正常推进。
        for (int pot = 1; pot <= 2; pot++)
        {
            FinishPot(gc);
            SettlePot(gc);
            Assert(!gc.IsAwaitingRouteChoice, $"第 {pot} 锅非章末不应提供路线");
            gc.AdvanceToNextPot();
        }

        // 第 3 锅（章末）。
        FinishPot(gc);
        Assert(!gc.IsAwaitingRouteChoice, "商店未结算前不应提供路线");

        // 先处理奖励 / 伙伴，保留商店营业。
        if (gc.IsAwaitingReward)
            gc.ChooseReward(gc.RewardCandidates[0].InstanceId);
        if (gc.IsAwaitingCompanionChoice)
            gc.SkipCompanionChoice();

        Assert(gc.IsShopOpen, "第 3 锅结束应进入商店营业态");
        Assert(!gc.IsAwaitingRouteChoice, "商店营业中不应提供路线");
        Assert(!gc.CanAdvanceToNextPot, "商店未结算前不可推进");

        // 商店结算 → 路线可选，且门控推进。
        gc.SkipShop();
        Assert(gc.IsAwaitingRouteChoice, "商店结算后应提供路线");
        Assert(gc.RouteOffers.Count == 3, $"路线候选应为 3 个，实际 {gc.RouteOffers.Count}");
        Assert(!gc.CanAdvanceToNextPot, "路线未处理前不可推进");

        // 选定路线 → 解锁推进。
        gc.ChooseRoute(gc.RouteOffers[0].Id);
        Assert(!gc.IsAwaitingRouteChoice, "选定后不应再提供路线");
        Assert(gc.CanAdvanceToNextPot, "路线处理后应可推进");
    }

    // ── 2. 候选构成：1 保底 + 2 风潮且不重复 ─────────────────────────────────

    static void Test_RouteCandidates_OneFallbackTwoTrends_Distinct()
    {
        var gc = NewController(seed: 8002);
        gc.StartNewGame();

        // 第 1、2 锅正常推进（非章末，无路线）。
        for (int pot = 1; pot <= 2; pot++)
        {
            FinishPot(gc);
            SettlePot(gc);
            gc.AdvanceToNextPot();
        }

        // 第 3 锅：结算奖励 → 伙伴 → 商店，保留路线候选。
        FinishPot(gc);
        if (gc.IsAwaitingReward)
            gc.ChooseReward(gc.RewardCandidates[0].InstanceId);
        if (gc.IsAwaitingCompanionChoice)
            gc.SkipCompanionChoice();
        if (gc.IsShopOpen)
            gc.SkipShop();

        Assert(gc.IsAwaitingRouteChoice, "应处于路线等待态");
        Assert(gc.RouteOffers.Count == 3, $"应为 3 个候选，实际 {gc.RouteOffers.Count}");
        Assert(gc.RouteOffers.Count(r => r.Kind == RouteKind.Fallback) == 1,
            "应恰好 1 个保底候选");
        Assert(gc.RouteOffers.Count(r => r.Kind == RouteKind.FlavorTrend) == 2,
            "应恰好 2 个风潮候选");

        var ids = gc.RouteOffers.Select(r => r.Id).ToList();
        Assert(ids.Distinct().Count() == ids.Count, "同一批候选不得重复");
    }

    // ── 3. 风潮倾斜：下一锅奖励池出现主题倾向 ────────────────────────────────

    static int CountSweetRewardHits(int seed, bool withTrend)
    {
        var gc = NewController(seed, potReward: new PotRewardConfig(), shop: NoShop());
        gc.StartNewGame();
        if (withTrend)
        {
            gc.Run.RouteId = "trend_sweet";
            gc.Run.RouteActiveChapter = gc.Run.Chapter;
        }

        gc.StartCurrentPot();
        FinishPot(gc);

        return gc.RewardCandidates.Count(c => c.Definition.Flavors.ContainsKey(FlavorType.Sweet));
    }

    static void Test_ActiveTrend_BiasesNextPotReward()
    {
        int baseline = 0;
        int withTrend = 0;
        const int seeds = 80;
        for (int seed = 0; seed < seeds; seed++)
        {
            baseline += CountSweetRewardHits(seed, withTrend: false);
            withTrend += CountSweetRewardHits(seed, withTrend: true);
        }

        Assert(withTrend > baseline,
            $"甜味风潮应提升甜味食材在奖励池的出现次数（基线 {baseline}，风潮 {withTrend}）");
    }

    /// <summary>统计一场固定种子下、奖励池中出现指定味道的食材数（可开关风潮）。</summary>
    static int CountFlavorRewardHits(int seed, bool withTrend, string routeId, FlavorType flavor)
    {
        var gc = NewController(seed, potReward: new PotRewardConfig(), shop: NoShop());
        gc.StartNewGame();
        if (withTrend)
        {
            gc.Run.RouteId = routeId;
            gc.Run.RouteActiveChapter = gc.Run.Chapter;
        }

        gc.StartCurrentPot();
        FinishPot(gc);

        return gc.RewardCandidates.Count(c => c.Definition.Flavors.ContainsKey(flavor));
    }

    /// <summary>
    /// 苦 / 咸 / 麻三条风潮必须真能倾斜（此前正式 Registry 无对应基础食材 → 权重退化为均匀的死选项）。
    /// </summary>
    static void Test_NewTrendRoutes_BiasRewardPool()
    {
        var cases = new (string RouteId, FlavorType Flavor)[]
        {
            ("trend_bitter", FlavorType.Bitter),
            ("trend_salty", FlavorType.Salty),
            ("trend_numbing", FlavorType.Numbing),
        };

        const int seeds = 80;
        foreach (var (routeId, flavor) in cases)
        {
            int baseline = 0;
            int withTrend = 0;
            for (int seed = 0; seed < seeds; seed++)
            {
                baseline += CountFlavorRewardHits(seed, withTrend: false, routeId, flavor);
                withTrend += CountFlavorRewardHits(seed, withTrend: true, routeId, flavor);
            }

            Assert(withTrend > baseline,
                $"{routeId} 应提升「{flavor}」食材在奖励池的出现次数（基线 {baseline}，风潮 {withTrend}）");
        }
    }

    // ── 4. 到期：跨过生效章节后风潮清除（恢复均匀，不叠加） ───────────────────

    static void Test_ActiveTrend_ExpiresAfterItsChapter()
    {
        var gc = NewController(seed: 8004);
        gc.StartNewGame();
        gc.Run.RouteId = "trend_sweet";
        gc.Run.RouteActiveChapter = 1;

        // 同章内推进（第 1、2 锅）不应清除风潮。
        for (int pot = 1; pot <= 2; pot++)
        {
            FinishPot(gc);
            SettlePot(gc);
            gc.AdvanceToNextPot();
            Assert(gc.Run.RouteId == "trend_sweet", $"同章推进后风潮应保留（第 {pot} 锅后）");
        }

        // 第 3 锅末：跳过路线（保底不写风潮状态），旧风潮仍在。
        FinishPot(gc);
        if (gc.IsAwaitingReward)
            gc.ChooseReward(gc.RewardCandidates[0].InstanceId);
        if (gc.IsAwaitingCompanionChoice)
            gc.SkipCompanionChoice();
        if (gc.IsShopOpen)
            gc.SkipShop();
        Assert(gc.IsAwaitingRouteChoice, "第 3 锅末应可跳过路线");
        gc.SkipRoute();
        Assert(gc.Run.RouteId == "trend_sweet", "保底路线不应改动风潮状态");

        // 跨章推进 → 越过生效章节（1），风潮到期清除。
        gc.AdvanceToNextPot();
        Assert(gc.Run.RouteId == null, "跨过生效章节后应清除风潮");
        Assert(gc.Run.RouteActiveChapter == 0, "清除后生效章节应归零");
    }

    // ── 5. 跳过 = 自动获得保底收益 ───────────────────────────────────────────

    static void Test_SkipRoute_GrantsFallbackReward()
    {
        var gc = NewController(seed: 8005);
        gc.StartNewGame();

        for (int pot = 1; pot <= 2; pot++)
        {
            FinishPot(gc);
            SettlePot(gc);
            gc.AdvanceToNextPot();
        }

        FinishPot(gc);
        if (gc.IsAwaitingReward)
            gc.ChooseReward(gc.RewardCandidates[0].InstanceId);
        if (gc.IsAwaitingCompanionChoice)
            gc.SkipCompanionChoice();
        if (gc.IsShopOpen)
            gc.SkipShop();

        Assert(gc.IsAwaitingRouteChoice, "应处于路线等待态");
        var fallback = gc.RouteOffers.Single(r => r.Kind == RouteKind.Fallback);

        int goldBefore = gc.Player.Gold;
        int basketBefore = gc.Player.IngredientBasket.Count;
        int itemsBefore = gc.Player.Items.Count;

        RouteChosenEvent? chosen = null;
        gc.Events.Subscribe<RouteChosenEvent>(e => chosen = e);

        gc.SkipRoute();

        Assert(chosen != null, "SkipRoute 应发布 RouteChosenEvent");
        Assert(chosen!.Skipped, "跳过事件的 Skipped 应为 true");
        Assert(chosen.Route.Id == fallback.Id, "跳过应执行本批候选里的保底项");
        Assert(gc.Player.Gold == goldBefore + fallback.GoldReward, "保底金币应如期发放");
        Assert(gc.Player.IngredientBasket.Count == basketBefore + fallback.IngredientRewardCount,
            "保底食材应如期入篮");
        Assert(gc.Player.Items.Count == itemsBefore + fallback.ItemRewardCount,
            "保底道具应如期入道具栏");
        Assert(gc.Run.RouteId == null, "保底路线不应写风潮状态");
        Assert(gc.CanAdvanceToNextPot, "处理后应可推进");
    }

    // ── 6. 第 3 / 6 / 9 锅各触发一次路线 ─────────────────────────────────────

    static void Test_RouteOfferedAtPots3_6_9()
    {
        var gc = NewController(seed: 8006);
        gc.StartNewGame();

        int offers = 0;
        gc.Events.Subscribe<RouteOfferedEvent>(_ => offers++);

        for (int pot = 1; pot <= 9; pot++)
        {
            FinishPot(gc);
            SettlePot(gc);
            if (pot < 9)
                gc.AdvanceToNextPot();
        }

        Assert(offers == 3, $"第 3/6/9 锅应各触发一次路线，共 3 次，实际 {offers}");

        // 第 9 锅末结算后推进 → 最终锅。
        gc.AdvanceToNextPot();
        Assert(gc.IsFinalPot, "第 9 锅后应进入最终锅");
    }

    // ── 7. 固定种子可复现候选 ────────────────────────────────────────────────

    static string RouteSignature(int seed)
    {
        var gc = NewController(seed);
        gc.StartNewGame();
        for (int pot = 1; pot <= 2; pot++)
        {
            FinishPot(gc);
            SettlePot(gc);
            gc.AdvanceToNextPot();
        }

        FinishPot(gc);
        if (gc.IsAwaitingReward)
            gc.ChooseReward(gc.RewardCandidates[0].InstanceId);
        if (gc.IsAwaitingCompanionChoice)
            gc.SkipCompanionChoice();
        if (gc.IsShopOpen)
            gc.SkipShop();

        return string.Join(",", gc.RouteOffers.Select(r => r.Id));
    }

    static void Test_RouteCandidates_ReproducibleWithFixedSeed()
    {
        Assert(RouteSignature(4242) == RouteSignature(4242),
            "相同种子下路线候选应完全可复现");
    }

    // ── R0 回归：默认 null 权重时与旧实现逐位一致 ─────────────────────────────

    static List<string> LegacyIngredientSequence(int count, int seed)
    {
        var r = new Random(seed);
        var all = IngredientData.Registry.GetAll().ToList();
        var result = new List<string>();
        int take = Math.Min(count, all.Count);
        for (int i = 0; i < take; i++)
        {
            int index = r.Next(all.Count);
            result.Add(all[index].Id);
            all.RemoveAt(index);
        }
        return result;
    }

    static List<string> LegacyItemSequence(int count, int seed)
    {
        var r = new Random(seed);
        var all = ItemData.Registry.GetAll().ToList();
        var result = new List<string>();
        int take = Math.Min(count, all.Count);
        for (int i = 0; i < take; i++)
        {
            int index = r.Next(all.Count);
            result.Add(all[index].Id);
            all.RemoveAt(index);
        }
        return result;
    }

    static void Test_R0_NullWeight_MatchesLegacyBehavior()
    {
        for (int seed = 0; seed < 30; seed++)
        {
            // 食材多抽：null 权重 = 旧实现；全等权重也必须回退到同一条均匀路径。
            var legacyIngredients = LegacyIngredientSequence(5, seed);
            var newIngredients = IngredientData.CreateRandomInstances(5, new Random(seed))
                .Select(i => i.Definition.Id).ToList();
            var equalWeightIngredients = IngredientData
                .CreateRandomInstances(5, new Random(seed), _ => 1.0)
                .Select(i => i.Definition.Id).ToList();
            Assert(legacyIngredients.SequenceEqual(newIngredients),
                $"seed {seed}: 食材多抽 null 权重应与旧实现一致");
            Assert(legacyIngredients.SequenceEqual(equalWeightIngredients),
                $"seed {seed}: 食材多抽全等权重应回退到均匀路径");

            // 食材单抽。
            var legacyOne = new Random(seed).Next(IngredientData.Registry.GetAll().Count);
            var legacyOneId = IngredientData.Registry.GetAll()[legacyOne].Id;
            Assert(IngredientData.CreateRandomInstance(new Random(seed)).Definition.Id == legacyOneId,
                $"seed {seed}: 食材单抽 null 权重应与旧实现一致");

            // 道具多抽。
            var legacyItems = LegacyItemSequence(3, seed);
            var newItems = ItemData.CreateRandomInstances(3, new Random(seed))
                .Select(i => i.Definition.Id).ToList();
            var equalWeightItems = ItemData.CreateRandomInstances(3, new Random(seed), _ => 2.5)
                .Select(i => i.Definition.Id).ToList();
            Assert(legacyItems.SequenceEqual(newItems),
                $"seed {seed}: 道具多抽 null 权重应与旧实现一致");
            Assert(legacyItems.SequenceEqual(equalWeightItems),
                $"seed {seed}: 道具多抽全等权重应回退到均匀路径");

            // IngredientPool 抽卡。
            var initial = LegacyIngredientSequence(6, seed)
                .Select(id => new IngredientInstance(IngredientData.Registry.Get(id)))
                .ToList();
            var legacyPoolRandom = new Random(seed);
            var legacyPool = new List<string>();
            var available = initial.ToList();
            for (int i = 0; i < 3; i++)
            {
                int index = legacyPoolRandom.Next(available.Count);
                legacyPool.Add(available[index].InstanceId);
                available.RemoveAt(index);
            }

            var pool = new IngredientPool(initial, new Random(seed));
            var drawn = pool.Draw(3).Select(i => i.InstanceId).ToList();
            Assert(legacyPool.SequenceEqual(drawn),
                $"seed {seed}: IngredientPool.Draw null 权重应与旧实现一致");

            var equalPool = new IngredientPool(initial, new Random(seed));
            var equalDrawn = equalPool.Draw(3, _ => 1.0).Select(i => i.InstanceId).ToList();
            Assert(legacyPool.SequenceEqual(equalDrawn),
                $"seed {seed}: IngredientPool.Draw 全等权重应回退到均匀路径");
        }
    }

    /// <summary>权重路径确实能倾斜：只给蜂蜜正权重时，单抽必为蜂蜜。</summary>
    static void Test_R0_WeightedDraw_CanBias()
    {
        for (int seed = 0; seed < 20; seed++)
        {
            var picked = IngredientData.CreateRandomInstance(
                new Random(seed), d => d.Id == "honey" ? 1.0 : 0.0);
            Assert(picked.Definition.Id == "honey",
                $"seed {seed}: 只保留蜂蜜权重时单抽应为蜂蜜，实际 {picked.Definition.Id}");
        }
    }
}
