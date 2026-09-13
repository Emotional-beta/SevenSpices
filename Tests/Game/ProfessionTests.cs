using SevenSpices.Core.Content;
using SevenSpices.Core.Events;
using SevenSpices.Core.Flavors;
using SevenSpices.Core.Flavors.Verbs;
using SevenSpices.Core.Game;
using SevenSpices.Core.Ingredients;
using SevenSpices.Core.Items;
using SevenSpices.Core.Professions;
using SevenSpices.Core.Scoring;

namespace SevenSpices.Tests.Game;

/// <summary>
/// 职业系统测试（方案 §六 验收）。
/// 覆盖：默认职业兜底 / 指定职业起始套装 / 7 条起手规则（三种形态）/
/// 职业专属内容隔离于随机池 / 换职业起手规则随之不同 / 事件。
/// </summary>
public static class ProfessionTests
{
    public static void RunAll()
    {
        Test_StartNewGame_NoProfession_UsesDefault();
        Test_StartNewGame_ChosenProfession_BasketAndItem();
        Test_StarterRules_Sour_InjectsSour();
        Test_StarterRules_Bitter_InjectsAgingPool();
        Test_StarterRules_Spicy_InjectsHeat();
        Test_StarterRules_Salty_Solidified();
        Test_StarterRules_Umami_RelaxesAbundanceRequirement();
        Test_Sweet_ExtraTriggerAfterSweet();
        Test_Numbing_ResonancePlusOne();
        Test_Numbing_ResonancePlusOne_DoesNotExceedCap();
        Test_ProfessionContent_NotInRandomPools();
        Test_SwitchProfession_ChangesStarterRules();
        Test_ProfessionChosenEvent_Published();

        Console.WriteLine("All ProfessionTests passed.");
    }

    // ── 辅助 ─────────────────────────────────────────────────────────────────

    static GameController NewGame(string? professionId = null)
    {
        var gc = new GameController(metaState: new MetaState());
        gc.StartNewGame(professionId);
        return gc;
    }

    static IProfessionVerbHook GetVerbHook(string professionId) =>
        ProfessionConfig.Get(professionId).Hooks.OfType<IProfessionVerbHook>().First();

    static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception($"[FAIL] ProfessionTests: {message}");
    }

    /// <summary>计数动词：记录每个味道的动词被触发次数，用于验证「额外触发」「共振次数」。</summary>
    sealed class CountingVerb : IFlavorVerb
    {
        private readonly Dictionary<FlavorType, int> _counts;

        public CountingVerb(Dictionary<FlavorType, int> counts) => _counts = counts;

        public void Apply(FlavorContext context)
        {
            _counts.TryGetValue(context.Flavor, out int count);
            _counts[context.Flavor] = count + 1;
        }
    }

    // ── 1. 默认职业兜底 ───────────────────────────────────────────────────────

    static void Test_StartNewGame_NoProfession_UsesDefault()
    {
        var gc = NewGame();

        Assert(gc.Run.ProfessionId == ProfessionConfig.Default.Id,
            $"不选职业应使用默认职业 '{ProfessionConfig.Default.Id}'，实际 '{gc.Run.ProfessionId}'");
        Assert(gc.Pot.Phase == PotPhase.InProgress, "不选职业也应成功开局（锅 InProgress）");
    }

    // ── 2. 指定职业起始套装 ───────────────────────────────────────────────────

    static void Test_StartNewGame_ChosenProfession_BasketAndItem()
    {
        foreach (var profession in ProfessionConfig.All)
        {
            var gc = NewGame(profession.Id);

            Assert(gc.Run.ProfessionId == profession.Id,
                $"{profession.Id}: Run.ProfessionId 应写入");

            var basket = gc.Player.IngredientBasket;
            Assert(basket.Count == 6, $"{profession.Id}: 起始篮应为 6 个，实际 {basket.Count}");
            Assert(basket.Count(i => i.Definition.Id == "rice") == 5,
                $"{profession.Id}: 起始篮应有 5 个米饭");
            Assert(basket.Count(i => i.Definition.Id == profession.StarterIngredientId) == 1,
                $"{profession.Id}: 起始篮应有 1 个专属食材 '{profession.StarterIngredientId}'");

            Assert(gc.Player.Items.Count == 1,
                $"{profession.Id}: 起始道具应为 1 个（无局外粉末），实际 {gc.Player.Items.Count}");
            Assert(gc.Player.Items[0].Definition.Id == profession.StarterItemId,
                $"{profession.Id}: 起始道具应为专属道具 '{profession.StarterItemId}'");
        }
    }

    // ── 3. 起手规则（三种形态） ───────────────────────────────────────────────

    static void Test_StarterRules_Sour_InjectsSour()
    {
        var gc = NewGame("sour");
        Assert(gc.Pot.GetFlavor(FlavorType.Sour) == ProfessionConfig.SourStartSour,
            $"酸职业开局酸底应为 {ProfessionConfig.SourStartSour}，实际 {gc.Pot.GetFlavor(FlavorType.Sour)}");
    }

    static void Test_StarterRules_Bitter_InjectsAgingPool()
    {
        var gc = NewGame("bitter");
        Assert(gc.Pot.AgingPool > 0,
            $"苦职业开局陈酿池应 > 0，实际 {gc.Pot.AgingPool}");
    }

    static void Test_StarterRules_Spicy_InjectsHeat()
    {
        var gc = NewGame("spicy");
        Assert(gc.Pot.HeatBowlsRemaining > 0,
            $"辣职业开局余温碗数应 > 0，实际 {gc.Pot.HeatBowlsRemaining}");
        Assert(gc.Pot.HeatBonusTiers > 0,
            $"辣职业开局余温档数应 > 0，实际 {gc.Pot.HeatBonusTiers}");
    }

    static void Test_StarterRules_Salty_Solidified()
    {
        var gc = NewGame("salty");
        Assert(gc.Pot.IsSolidified, "咸职业开局应已固化");
    }

    /// <summary>
    /// 鲜职业：丰盛判定的味道种类门槛被覆盖为 1（默认职业为 2），使「恰好 1 种味道」也能触发丰盛倍率。
    /// <para>
    /// 公式为 <c>1 + AbundancePerType × (typeCount - 门槛 + 1)</c>：
    /// 鲜职业门槛 1、恰好 1 种味时 → <c>1 + 0.1 × 1 = 1.1 &gt; 1.0</c>（真正生效）；
    /// 非鲜职业门槛仍为 2、同样状态 → 不触发，倍率 = 1.0。
    /// </para>
    /// </summary>
    static void Test_StarterRules_Umami_RelaxesAbundanceRequirement()
    {
        var umami = NewGame("umami");
        Assert(umami.Pot.AbundanceFlavorTypeRequirement == ProfessionConfig.UmamiAbundanceRequirement,
            $"鲜职业丰盛门槛应为 {ProfessionConfig.UmamiAbundanceRequirement}，实际 {umami.Pot.AbundanceFlavorTypeRequirement}");

        // 恰好 1 种激活味道：鲜职业门槛放宽到 1 → 丰盛倍率应 > 1.0（公式真正生效）。
        umami.Pot.Flavors.Clear();
        umami.Pot.AddFlavor(FlavorType.Umami, 1);
        Assert(umami.Pot.ActiveFlavorTypeCount == 1,
            $"应恰好 1 种激活味道，实际 {umami.Pot.ActiveFlavorTypeCount}");
        double umamiMultiplier = ScoreCalculator.GetAbundanceMultiplier(umami.Pot);
        Assert(umamiMultiplier > 1.0,
            $"鲜职业恰好 1 种味道时丰盛倍率应 > 1.0，实际 {umamiMultiplier}");

        // 非鲜职业：门槛仍为 2，同样「恰好 1 种味」时不应触发，倍率保持 1.0。
        var sour = NewGame("sour");
        Assert(sour.Pot.AbundanceFlavorTypeRequirement == 2,
            $"默认/其它职业丰盛门槛应保持 2，实际 {sour.Pot.AbundanceFlavorTypeRequirement}");
        sour.Pot.Flavors.Clear();
        sour.Pot.AddFlavor(FlavorType.Umami, 1);
        Assert(sour.Pot.ActiveFlavorTypeCount == 1,
            $"非鲜职业应恰好 1 种激活味道，实际 {sour.Pot.ActiveFlavorTypeCount}");
        double sourMultiplier = ScoreCalculator.GetAbundanceMultiplier(sour.Pot);
        Assert(sourMultiplier == 1.0,
            $"非鲜职业恰好 1 种味道时丰盛倍率应仍为 1.0，实际 {sourMultiplier}");
    }

    /// <summary>
    /// 甜职业动词联动：主循环结算甜动词后，额外再触发一次「当前最高味」的动词。
    /// 用计数动词验证：鲜动词本应只被主循环触发 1 次，甜职业下应变为 2 次。
    /// </summary>
    static void Test_Sweet_ExtraTriggerAfterSweet()
    {
        var ingredient = new IngredientInstance(new IngredientDefinition(
            id: "test_sweet_umami",
            name: "测试甜鲜",
            rarity: IngredientRarity.Common,
            baseScore: 1,
            flavors: new() { [FlavorType.Sweet] = 1, [FlavorType.Umami] = 1 }));

        // 对照组：不接职业联动 → 鲜动词只被主循环触发 1 次。
        var baselineCounts = new Dictionary<FlavorType, int>();
        var baseline = new FlavorInteractionSystem(new Dictionary<FlavorType, IFlavorVerb>
        {
            [FlavorType.Sweet] = new DuplicateVerb(),
            [FlavorType.Umami] = new CountingVerb(baselineCounts),
        });
        var baselinePot = new PotState();
        baselinePot.AddFlavor(FlavorType.Umami, 3);
        baseline.Resolve(baselinePot, ingredient, baseScoreAdded: 1);
        Assert(baselineCounts.GetValueOrDefault(FlavorType.Umami) == 1,
            $"对照：无职业联动时鲜动词应触发 1 次，实际 {baselineCounts.GetValueOrDefault(FlavorType.Umami)}");

        // 实验组：甜职业联动 → 甜动词结算后额外触发最高味（鲜）一次。
        var counts = new Dictionary<FlavorType, int>();
        var system = new FlavorInteractionSystem(new Dictionary<FlavorType, IFlavorVerb>
        {
            [FlavorType.Sweet] = new DuplicateVerb(),
            [FlavorType.Umami] = new CountingVerb(counts),
        });
        var pot = new PotState();
        pot.AddFlavor(FlavorType.Umami, 3);
        pot.VerbLink = GetVerbHook("sweet");
        system.Resolve(pot, ingredient, baseScoreAdded: 1);

        Assert(counts.GetValueOrDefault(FlavorType.Umami) == 2,
            $"甜职业：鲜动词应被触发 2 次（主循环 1 + 额外 1），实际 {counts.GetValueOrDefault(FlavorType.Umami)}");
    }

    /// <summary>
    /// 麻职业动词联动：麻·共振触发次数 +1（「再响一次」）。
    /// 锅中有鲜=2 时默认共振 2 次，麻职业下应为 3 次。
    /// </summary>
    static void Test_Numbing_ResonancePlusOne()
    {
        var ingredient = new IngredientInstance(new IngredientDefinition(
            id: "test_numbing",
            name: "测试麻",
            rarity: IngredientRarity.Common,
            baseScore: 1,
            flavors: new() { [FlavorType.Numbing] = 1 }));

        // 对照组：不接职业联动 → 共振按最高味份数（鲜=2）触发 2 次。
        var baselineCounts = new Dictionary<FlavorType, int>();
        var baseline = new FlavorInteractionSystem(new Dictionary<FlavorType, IFlavorVerb>
        {
            [FlavorType.Umami] = new CountingVerb(baselineCounts),
        });
        var baselinePot = new PotState();
        baselinePot.AddFlavor(FlavorType.Umami, 2);
        baseline.Resolve(baselinePot, ingredient, baseScoreAdded: 1);
        Assert(baselineCounts.GetValueOrDefault(FlavorType.Umami) == 2,
            $"对照：无职业联动时共振应触发 2 次，实际 {baselineCounts.GetValueOrDefault(FlavorType.Umami)}");

        // 实验组：麻职业 → 触发次数 +1，应变为 3 次。
        var counts = new Dictionary<FlavorType, int>();
        var system = new FlavorInteractionSystem(new Dictionary<FlavorType, IFlavorVerb>
        {
            [FlavorType.Umami] = new CountingVerb(counts),
        });
        var pot = new PotState();
        pot.AddFlavor(FlavorType.Umami, 2);
        pot.VerbLink = GetVerbHook("numbing");
        system.Resolve(pot, ingredient, baseScoreAdded: 1);

        Assert(counts.GetValueOrDefault(FlavorType.Umami) == 3,
            $"麻职业：共振应触发 3 次（2 + 1），实际 {counts.GetValueOrDefault(FlavorType.Umami)}");
    }

    /// <summary>
    /// 麻职业「再响一次」不得突破共振封顶：封顶 2 且最高味 2 时，+1 后应夹回 2 而非 3。
    /// </summary>
    static void Test_Numbing_ResonancePlusOne_DoesNotExceedCap()
    {
        var ingredient = new IngredientInstance(new IngredientDefinition(
            id: "test_numbing_cap",
            name: "测试麻封顶",
            rarity: IngredientRarity.Common,
            baseScore: 1,
            flavors: new() { [FlavorType.Numbing] = 1 }));

        var counts = new Dictionary<FlavorType, int>();
        var system = new FlavorInteractionSystem(new Dictionary<FlavorType, IFlavorVerb>
        {
            [FlavorType.Umami] = new CountingVerb(counts),
        });
        var pot = new PotState { Config = new FlavorConfig { NumbingResonanceCap = 2 } };
        pot.AddFlavor(FlavorType.Umami, 2);
        pot.VerbLink = GetVerbHook("numbing");
        system.Resolve(pot, ingredient, baseScoreAdded: 1);

        Assert(counts.GetValueOrDefault(FlavorType.Umami) == 2,
            $"麻职业 +1 不得突破共振封顶 2，实际 {counts.GetValueOrDefault(FlavorType.Umami)}");
    }

    // ── 4. 职业专属内容隔离于随机池 ───────────────────────────────────────────

    static void Test_ProfessionContent_NotInRandomPools()
    {
        var ingredientIds = ProfessionConfig.All.Select(p => p.StarterIngredientId).ToHashSet();
        var itemIds = ProfessionConfig.All.Select(p => p.StarterItemId).ToHashSet();

        Assert(ingredientIds.Count == 7, "应有 7 个互不相同的职业专属食材");
        Assert(itemIds.Count == 7, "应有 7 个互不相同的职业专属道具");

        // 专属 Registry 能取到全部 7 条内容。
        foreach (var id in ingredientIds)
            Assert(IngredientData.ProfessionRegistry.Get(id).Id == id, $"ProfessionRegistry 应含食材 '{id}'");
        foreach (var id in itemIds)
            Assert(ItemData.ProfessionRegistry.Get(id).Id == id, $"ProfessionRegistry 应含道具 '{id}'");

        // 多次随机抽取，专属内容永不出现。
        for (int seed = 0; seed < 200; seed++)
        {
            var random = new Random(seed);

            foreach (var instance in IngredientData.CreateRandomInstances(99, random))
                Assert(!ingredientIds.Contains(instance.Definition.Id),
                    $"专属食材 '{instance.Definition.Id}' 不应出现在随机食材池");

            Assert(!ingredientIds.Contains(IngredientData.CreateRandomInstance(random).Definition.Id),
                "专属食材不应出现在随机食材掉落");

            foreach (var instance in ItemData.CreateRandomInstances(99, random))
                Assert(!itemIds.Contains(instance.Definition.Id),
                    $"专属道具 '{instance.Definition.Id}' 不应出现在随机道具池");

            Assert(!itemIds.Contains(ItemData.CreateRandomInstance(random).Definition.Id),
                "专属道具不应出现在随机道具掉落");
        }
    }

    // ── 5. 换职业 → 起手规则随之不同 ─────────────────────────────────────────

    static void Test_SwitchProfession_ChangesStarterRules()
    {
        var gc = new GameController(metaState: new MetaState());

        gc.StartNewGame("salty");
        Assert(gc.Pot.IsSolidified, "咸职业开局应固化");
        Assert(gc.Pot.GetFlavor(FlavorType.Sour) == 0, "咸职业不应带酸底");

        gc.StartNewGame("sour");
        Assert(!gc.Pot.IsSolidified, "换成酸职业后不应再固化");
        Assert(gc.Pot.GetFlavor(FlavorType.Sour) == ProfessionConfig.SourStartSour,
            "换成酸职业后应重新带酸底");

        gc.StartNewGame("umami");
        Assert(gc.Pot.AbundanceFlavorTypeRequirement == 1, "换成鲜职业后门槛应为 1");

        gc.StartNewGame("bitter");
        Assert(gc.Pot.AbundanceFlavorTypeRequirement == 2, "换成苦职业后门槛应复位为 2");
        Assert(gc.Pot.AgingPool > 0, "换成苦职业后陈酿池应 > 0");
    }

    // ── 6. 事件 ───────────────────────────────────────────────────────────────

    static void Test_ProfessionChosenEvent_Published()
    {
        var gc = new GameController(metaState: new MetaState());
        ProfessionChosenEvent? received = null;
        gc.Events.Subscribe<ProfessionChosenEvent>(e => received = e);

        gc.StartNewGame("spicy");

        Assert(received != null, "StartNewGame 应发布 ProfessionChosenEvent");
        Assert(received!.ProfessionId == "spicy",
            $"ProfessionChosenEvent.ProfessionId 应为 'spicy'，实际 '{received.ProfessionId}'");
    }
}
