using SevenSpices.Core.Content;
using SevenSpices.Core.Effects;
using SevenSpices.Core.Flavors;
using SevenSpices.Core.Game;
using SevenSpices.Core.Ingredients;
using SevenSpices.Core.Pot;
using SevenSpices.Core.Scoring;

namespace SevenSpices.Tests.Flavors;

/// <summary>
/// 味道系统 F3 测试：苦·陈酿 / 辣·余温 / 鲜·提鲜 / 咸·固化 / 麻·共振，
/// 以及跨碗状态快照与最终锅陈酿立即兑现。
/// </summary>
public static class ComplexVerbTests
{
    public static void RunAll()
    {
        Test_Aging_DepositCompoundMatureAndClear();
        Test_FinalPot_EndCooking_RealizesAgingPoolImmediately();
        Test_Heat_ConsumesSpicyAndBoostsMultiplier();
        Test_Heat_AccumulatesBelowCost_ThenTriggers();
        Test_Heat_DecrementsPerNextBowl_AndDoesNotStack();
        Test_FinalPot_Heat_DoesNotBoostMultiplier();
        Test_Umami_MultipliesNonUmamiOnly();
        Test_Umami_GrowsWithFlavorTypeCount_AndCaps();
        Test_Salty_Solidifies_AndEtchSkips();
        Test_Salty_SolidifyBeforeEtch_PersistsAcrossBowls();
        Test_Numbing_Resonance_TriggersByPortionAndCap();
        Test_Numbing_Resonance_TargetsHighestRegisteredFlavor();
        Test_Numbing_Resonance_NoRegisteredTarget_NoAction();
        Test_Numbing_Resonance_NoInfiniteSelfTrigger();
        Test_Numbing_NotRegisteredAsNormalVerb();
        Test_Preview_MatchesReal_Aging();
        Test_Preview_MatchesReal_Heat();
        Test_Preview_MatchesReal_Umami();
        Test_Preview_SnapshotDeepCopiesF3State();

        Console.WriteLine("All ComplexVerbTests passed.");
    }

    // ── 测试用动词 ───────────────────────────────────────────────────────────

    private sealed class CountingVerb : IFlavorVerb
    {
        public int ApplyCount { get; private set; }

        public void Apply(FlavorContext context) => ApplyCount++;
    }

    /// <summary>每次结算都 +1 麻：用于验证共振不会因麻增长而无限自我触发。</summary>
    private sealed class NumbingGrowingVerb : IFlavorVerb
    {
        public int ApplyCount { get; private set; }

        public void Apply(FlavorContext context)
        {
            ApplyCount++;
            context.Pot.AddFlavor(FlavorType.Numbing, 1);
        }
    }

    // ── 辅助 ─────────────────────────────────────────────────────────────────

    static PotController MakeControllerAtIngredientResolve(
        out GameState state, FlavorInteractionSystem? system = null, FlavorConfig? config = null)
    {
        state = new GameState();
        var ctrl = new PotController(state, flavorInteraction: system, flavorConfig: config);
        ctrl.StartPot();
        ctrl.StartBowl();
        ctrl.AdvanceBowlPhase(); // → Customer
        ctrl.AdvanceBowlPhase(); // → ItemPhase
        ctrl.AdvanceBowlPhase(); // → IngredientSelection
        ctrl.AdvanceBowlPhase(); // → IngredientResolve
        return ctrl;
    }

    static IngredientInstance MakeIngredient(int baseScore, params (FlavorType Flavor, int Amount)[] flavors)
    {
        var dict = new Dictionary<FlavorType, int>();
        foreach (var (flavor, amount) in flavors)
            dict[flavor] = amount;

        var def = new IngredientDefinition(
            id: "test_" + Guid.NewGuid().ToString("N"),
            name: "测试食材",
            rarity: IngredientRarity.Common,
            baseScore: baseScore,
            flavors: dict);
        return new IngredientInstance(def);
    }

    static FlavorInteractionSystem SystemWith(params (FlavorType Flavor, IFlavorVerb Verb)[] verbs)
    {
        var dict = new Dictionary<FlavorType, IFlavorVerb>();
        foreach (var (flavor, verb) in verbs)
            dict[flavor] = verb;
        return new FlavorInteractionSystem(dict);
    }

    static void AdvanceBowlToEnd(PotController ctrl)
    {
        while (ctrl.Pot.CurrentBowlPhase != BowlPhase.End && ctrl.Pot.Phase == PotPhase.InProgress)
            ctrl.AdvanceBowlPhase();
    }

    // ── 1. 苦·陈酿 ───────────────────────────────────────────────────────────

    static void Test_Aging_DepositCompoundMatureAndClear()
    {
        var ctrl = MakeControllerAtIngredientResolve(out var state); // 默认：0.5 / 0.1 / 3
        var es = new EffectSystem();

        // 加苦（基础分 10）→ 存入 5 后当次即复利 ×1.1 = 5.5，计时 1。
        ctrl.AddIngredient(MakeIngredient(10, (FlavorType.Bitter, 1)), es);
        Assert(state.Pot.BaseScore == 10, "加苦后基础分应先计入 10");
        AssertClose(state.Pot.AgingPool, 5.5, "存入 10×0.5=5 后当次复利 ×1.1 = 5.5");
        Assert(state.Pot.AgingAdds == 1, "存入一次后计数应为 1");

        // 第二次加料（无苦）→ 池复利 5.5 × 1.1 = 6.05。
        ctrl.AddIngredient(MakeIngredient(0), es);
        AssertClose(state.Pot.AgingPool, 6.05, "此后每次加料应复利 ×1.1");
        Assert(state.Pot.AgingAdds == 2, "第二次加料后计数应为 2");

        // 第三次加料 → 池 6.05 × 1.1 = 6.655，达到 3 次到期，floor(6.655)=6 并入基础分并清空。
        ctrl.AddIngredient(MakeIngredient(0), es);
        Assert(state.Pot.BaseScore == 16, "到期应把 floor(6.655)=6 并入 BaseScore（10+6）");
        Assert(state.Pot.AgingPool == 0, "到期兑现后陈酿池应清空");
        Assert(state.Pot.AgingAdds == 0, "到期兑现后计数应复位");

        // 兑现分数参与算分。仅苦 1 种味道 → F4 寡淡 ×0.9。
        Assert(ScoreCalculator.ComputeFinalScore(state.Pot) == 15,
            "BaseScore=16、苦味道分=1、第1碗×1、仅苦 1 种寡淡×0.9 → floor(17×0.9) = 15");
    }

    // ── 2. 最终锅立即兑现 ────────────────────────────────────────────────────

    static void Test_FinalPot_EndCooking_RealizesAgingPoolImmediately()
    {
        var state = new GameState();
        var gc = new GameController(state, random: new Random(99));
        gc.StartNewGame();

        // 切到最终锅并重建本锅池，篮中放一个苦味食材（基础分 10，苦+1）。
        state.Run.IsFinalPot = true;
        state.Player.IngredientBasket.Clear();
        state.Player.IngredientBasket.Add(MakeIngredient(10, (FlavorType.Bitter, 1)));
        gc.StartCurrentPot();

        Assert(gc.CanSelectIngredient, "最终锅应可投入食材");
        gc.SelectIngredient(gc.CurrentCandidates[0].InstanceId);

        double pool = state.Pot.AgingPool;
        Assert(pool > 0, "加苦后最终锅应产生未兑现陈酿池（未到期）");
        Assert(state.Pot.BaseScore == 10, "结算前基础分仅为食材基础分");

        gc.EndCooking();

        Assert(state.Pot.AgingPool == 0, "最终锅结算应清空陈酿池");
        Assert(state.Pot.BaseScore == 10 + (int)Math.Floor(pool),
            "最终锅结算前应立即全额兑现陈酿池进 BaseScore");
        Assert(state.Pot.FinalScore == ScoreCalculator.ComputeFinalScore(state.Pot),
            "最终分应包含立即兑现的陈酿分数（与算分公式一致）");
    }

    // ── 3. 辣·余温 ───────────────────────────────────────────────────────────

    static void Test_Heat_ConsumesSpicyAndBoostsMultiplier()
    {
        var ctrl = MakeControllerAtIngredientResolve(out var state); // BowlNumber=1，基础倍率 ×1
        state.Pot.AddFlavor(FlavorType.Spicy, 10);

        ctrl.AddIngredient(MakeIngredient(0, (FlavorType.Spicy, 1)), new EffectSystem());

        Assert(state.Pot.GetFlavor(FlavorType.Spicy) == 8, "辣应消耗 HeatCost=3：11 - 3 = 8");
        Assert(state.Pot.HeatBowlsRemaining == 2, "余温应持续 2 碗");
        Assert(state.Pot.HeatBonusTiers == 1, "余温应提高 1 档");
        Assert(ScoreCalculator.GetEffectiveMultiplier(state.Pot) == 2,
            "第1碗 ×1 + 余温 1 档 = ×2");
    }

    static void Test_Heat_AccumulatesBelowCost_ThenTriggers()
    {
        var ctrl = MakeControllerAtIngredientResolve(out var state); // BowlNumber=1，基础倍率 ×1

        // 第 1 次加辣 +1 → 辣=1 < HeatCost=3：不点火、不消耗。
        ctrl.AddIngredient(MakeIngredient(0, (FlavorType.Spicy, 1)), new EffectSystem());
        Assert(state.Pot.GetFlavor(FlavorType.Spicy) == 1, "辣=1 < HeatCost 应累积不消耗");
        Assert(state.Pot.HeatBowlsRemaining == 0 && state.Pot.HeatBonusTiers == 0, "辣不足时不应设置余温");
        Assert(ScoreCalculator.GetEffectiveMultiplier(state.Pot) == 1, "未点火时倍率应为基础 ×1");

        // 第 2 次加辣 +1 → 辣=2 < HeatCost：仍不点火。
        ctrl.AddIngredient(MakeIngredient(0, (FlavorType.Spicy, 1)), new EffectSystem());
        Assert(state.Pot.GetFlavor(FlavorType.Spicy) == 2, "辣=2 < HeatCost 应继续累积");
        Assert(state.Pot.HeatBowlsRemaining == 0 && state.Pot.HeatBonusTiers == 0, "辣仍不足时不应设置余温");
        Assert(ScoreCalculator.GetEffectiveMultiplier(state.Pot) == 1, "第二次仍未点火，倍率应为基础 ×1");

        // 第 3 次加辣 +1 → 辣=3 ≥ HeatCost：触发，正好消耗 3 → 辣=0。
        ctrl.AddIngredient(MakeIngredient(0, (FlavorType.Spicy, 1)), new EffectSystem());
        Assert(state.Pot.GetFlavor(FlavorType.Spicy) == 0, "辣达 3 应触发并正好消耗 HeatCost=3 → 辣=0");
        Assert(state.Pot.HeatBowlsRemaining == 2, "触发后余温应持续 2 碗");
        Assert(state.Pot.HeatBonusTiers == 1, "触发后余温应提高 1 档");
        Assert(ScoreCalculator.GetEffectiveMultiplier(state.Pot) == 2, "第1碗 ×1 + 余温 1 档 = ×2");
    }

    static void Test_Heat_DecrementsPerNextBowl_AndDoesNotStack()
    {
        var ctrl = MakeControllerAtIngredientResolve(out var state);
        state.Pot.AddFlavor(FlavorType.Spicy, 3);

        // 加辣 +3 → 辣=6 ≥ HeatCost=3 → 消耗 3 → 辣=3，触发。
        ctrl.AddIngredient(MakeIngredient(0, (FlavorType.Spicy, 3)), new EffectSystem());
        Assert(state.Pot.GetFlavor(FlavorType.Spicy) == 3, "触发后辣应正好消耗 HeatCost=3：6 - 3 = 3");
        Assert(state.Pot.HeatBowlsRemaining == 2, "首次触发后剩余 2 碗");

        // 不叠加：再次加辣（辣回到 6）只重置剩余碗数、取更高档位（此处仍为 1），不会变成 2 档。
        ctrl.AddIngredient(MakeIngredient(0, (FlavorType.Spicy, 3)), new EffectSystem());
        Assert(state.Pot.GetFlavor(FlavorType.Spicy) == 3, "再次触发应再次正好消耗 HeatCost=3");
        Assert(state.Pot.HeatBowlsRemaining == 2, "再次触发应重置剩余碗数而非叠加");
        Assert(state.Pot.HeatBonusTiers == 1, "档位不应叠加（仍为 1）");

        // 进入下一碗递减。
        AdvanceBowlToEnd(ctrl);
        ctrl.StartNextBowl();
        Assert(state.Pot.HeatBowlsRemaining == 1, "进入下一碗应递减为 1");

        AdvanceBowlToEnd(ctrl);
        ctrl.StartNextBowl();
        Assert(state.Pot.HeatBowlsRemaining == 0, "第二碗后应递减为 0");
        Assert(ScoreCalculator.GetEffectiveMultiplier(state.Pot) == ScoreCalculator.GetMultiplier(state.Pot.BowlNumber),
            "余温用尽后不应再额外加成");
    }

    static void Test_FinalPot_Heat_DoesNotBoostMultiplier()
    {
        // 最终锅：StartPot 把 BowlNumber 置为 10（×32），且余温永不递减。
        var state = new GameState();
        state.Run.IsFinalPot = true;
        var ctrl = new PotController(state);
        ctrl.StartPot();
        ctrl.StartBowl();
        ctrl.AdvanceBowlPhase(); // → Customer
        ctrl.AdvanceBowlPhase(); // → ItemPhase
        ctrl.AdvanceBowlPhase(); // → IngredientSelection
        ctrl.AdvanceBowlPhase(); // → IngredientResolve

        state.Pot.AddFlavor(FlavorType.Spicy, 10);
        ctrl.AddIngredient(MakeIngredient(0, (FlavorType.Spicy, 1)), new EffectSystem());

        Assert(state.Pot.IsFinalPot, "最终锅应标记 IsFinalPot");
        Assert(state.Pot.GetFlavor(FlavorType.Spicy) == 11, "最终锅不应消耗辣值");
        Assert(state.Pot.HeatBowlsRemaining == 0 && state.Pot.HeatBonusTiers == 0,
            "最终锅不应设置余温状态");
        Assert(ScoreCalculator.GetMultiplier(state.Pot.BowlNumber) == 32, "最终锅基础倍率应为 ×32");
        Assert(ScoreCalculator.GetEffectiveMultiplier(state.Pot) == 32,
            "最终锅实际倍率应固定 ×32，不含余温加成");
    }

    // ── 4. 鲜·提鲜 ───────────────────────────────────────────────────────────

    static void Test_Umami_MultipliesNonUmamiOnly()
    {
        var pot = new PotState();
        pot.AddFlavor(FlavorType.Umami, 1);
        pot.AddFlavor(FlavorType.Sweet, 4);

        // 加鲜触发味道互动结算（直接 Resolve，不重复应用基础味道）。
        FlavorInteractionSystem.Default.Resolve(
            pot, MakeIngredient(0, (FlavorType.Umami, 1)));

        AssertClose(pot.UmamiMultiplier, 1.2, "2 种味道 → 系数 1 + 0.2 = 1.2");
        // 鲜自身不乘：1 + 4×1.2 = 5.8（若鲜也乘则为 1.2 + 4.8 = 6.0）。
        AssertClose(pot.FlavorScore, 5.8, "鲜自身不参与提鲜乘算，非鲜味道分 ×1.2");
    }

    static void Test_Umami_GrowsWithFlavorTypeCount_AndCaps()
    {
        // 提鲜系数为派生只读：随味道种类数即时变化，无需任何刷新调用。
        // 种类数增长：2 种 → 1.2；3 种 → 1.4。
        var pot = new PotState();
        pot.AddFlavor(FlavorType.Umami, 1);
        pot.AddFlavor(FlavorType.Sweet, 1);
        AssertClose(pot.UmamiMultiplier, 1.2, "2 种味道 → 1.2");

        pot.AddFlavor(FlavorType.Bitter, 1);
        AssertClose(pot.UmamiMultiplier, 1.4, "3 种味道 → 1.4");

        // 封顶。
        var config = new FlavorConfig { UmamiBonusPerType = 1.0, UmamiMaxMultiplier = 2.0 };
        var capped = new PotState { Config = config };
        capped.AddFlavor(FlavorType.Umami, 1);
        capped.AddFlavor(FlavorType.Sweet, 1);
        capped.AddFlavor(FlavorType.Bitter, 1);
        capped.AddFlavor(FlavorType.Salty, 1);
        AssertClose(capped.UmamiMultiplier, 2.0, "系数应按 UmamiMaxMultiplier 封顶");

        // 鲜为 0 时系数为 1。
        var noUmami = new PotState();
        noUmami.AddFlavor(FlavorType.Sweet, 5);
        AssertClose(noUmami.UmamiMultiplier, 1.0, "鲜为 0 时系数应为 1");
        AssertClose(noUmami.FlavorScore, 5.0, "鲜为 0 时不应放大其它味道分");
    }

    // ── 5. 咸·固化 ───────────────────────────────────────────────────────────

    static void Test_Salty_Solidifies_AndEtchSkips()
    {
        var ctrl = MakeControllerAtIngredientResolve(out var state);
        var es = new EffectSystem();

        // 先加咸 → 固化。
        ctrl.AddIngredient(MakeIngredient(0, (FlavorType.Salty, 1)), es);
        Assert(state.Pot.IsSolidified, "加咸后本锅应固化");

        state.Pot.AddFlavor(FlavorType.Umami, 5);

        // 固化后加酸：蚀刻应被跳过，鲜不应被转移。
        ctrl.AddIngredient(MakeIngredient(0, (FlavorType.Sour, 1)), es);
        Assert(state.Pot.GetFlavor(FlavorType.Umami) == 5, "固化时酸·蚀刻应跳过：鲜不应被蚀");
        Assert(state.Pot.GetFlavor(FlavorType.Sour) == 1, "酸只应保留自身基础 +1");

        // 对照：未固化时先酸后咸，酸正常蚀刻。
        var ctrl2 = MakeControllerAtIngredientResolve(out var state2);
        state2.Pot.AddFlavor(FlavorType.Umami, 5);
        ctrl2.AddIngredient(MakeIngredient(0, (FlavorType.Sour, 1)), es);
        Assert(state2.Pot.GetFlavor(FlavorType.Umami) == 0, "未固化时蚀刻应把鲜转移给酸");
        Assert(state2.Pot.GetFlavor(FlavorType.Sour) == 6, "未固化时酸应获得 1 + 5 = 6");
    }

    static void Test_Salty_SolidifyBeforeEtch_PersistsAcrossBowls()
    {
        var ctrl = MakeControllerAtIngredientResolve(out var state);
        var es = new EffectSystem();

        ctrl.AddIngredient(MakeIngredient(0, (FlavorType.Salty, 1)), es);
        Assert(state.Pot.IsSolidified, "加咸后应固化");

        AdvanceBowlToEnd(ctrl);
        ctrl.StartNextBowl();
        Assert(state.Pot.IsSolidified, "固化应随本锅剩余时间存活，跨碗不失效");

        // 新碗中加酸仍应被跳过。
        AdvanceBowlToEnd(ctrl); // 该碗空走
        ctrl.StartNextBowl();
        while (state.Pot.CurrentBowlPhase != BowlPhase.IngredientResolve
               && state.Pot.Phase == PotPhase.InProgress)
            ctrl.AdvanceBowlPhase();

        state.Pot.AddFlavor(FlavorType.Umami, 3);
        ctrl.AddIngredient(MakeIngredient(0, (FlavorType.Sour, 1)), es);
        Assert(state.Pot.GetFlavor(FlavorType.Umami) == 3, "跨碗固化后蚀刻仍应跳过");
    }

    // ── 6. 麻·共振 ───────────────────────────────────────────────────────────

    static void Test_Numbing_Resonance_TriggersByPortionAndCap()
    {
        var verb = new CountingVerb();
        var system = SystemWith((FlavorType.Spicy, verb));

        // 最高味道辣=5，封顶 3 → 触发 3 次。
        var pot = new PotState { Config = new FlavorConfig { NumbingResonanceCap = 3 } };
        pot.AddFlavor(FlavorType.Spicy, 5);
        system.Resolve(pot, MakeIngredient(0, (FlavorType.Numbing, 1)));
        Assert(verb.ApplyCount == 3, $"应按份触发但受封顶限制为 3 次，实际 {verb.ApplyCount}");

        // 最高味道辣=2，封顶 10 → 按份触发 2 次。
        var verb2 = new CountingVerb();
        var system2 = SystemWith((FlavorType.Spicy, verb2));
        var pot2 = new PotState { Config = new FlavorConfig { NumbingResonanceCap = 10 } };
        pot2.AddFlavor(FlavorType.Spicy, 2);
        system2.Resolve(pot2, MakeIngredient(0, (FlavorType.Numbing, 1)));
        Assert(verb2.ApplyCount == 2, $"未达封顶时应按最高味道份数触发 2 次，实际 {verb2.ApplyCount}");
    }

    static void Test_Numbing_Resonance_TargetsHighestRegisteredFlavor()
    {
        // 只注册辣；麻值最高但没有普通动词，共振应改取「有注册动词的味道」中的最高（辣 2）。
        var verb = new CountingVerb();
        var system = SystemWith((FlavorType.Spicy, verb));

        var pot = new PotState { Config = new FlavorConfig { NumbingResonanceCap = 10 } };
        pot.AddFlavor(FlavorType.Numbing, 9);
        pot.AddFlavor(FlavorType.Spicy, 2);
        system.Resolve(pot, MakeIngredient(0, (FlavorType.Numbing, 1)));

        Assert(verb.ApplyCount == 2,
            $"共振应在已注册动词的味道中取最高（辣 2）并触发 2 次，实际 {verb.ApplyCount}");
    }

    static void Test_Numbing_Resonance_NoRegisteredTarget_NoAction()
    {
        // 没有任何已注册普通动词 → 共振空转，不抛异常、不改状态。
        var system = SystemWith();
        var pot = new PotState();
        pot.AddFlavor(FlavorType.Numbing, 5);
        system.Resolve(pot, MakeIngredient(0, (FlavorType.Numbing, 1)));

        Assert(pot.GetFlavor(FlavorType.Numbing) == 5, "无注册动词时共振应空转，麻值不变");
    }

    static void Test_Numbing_Resonance_NoInfiniteSelfTrigger()
    {
        // 目标动词每次都会增加麻值；共振作为独立步骤只按份触发有限次数，不得递归再共振。
        var verb = new NumbingGrowingVerb();
        var system = SystemWith((FlavorType.Spicy, verb));

        var pot = new PotState { Config = new FlavorConfig { NumbingResonanceCap = 3 } };
        pot.AddFlavor(FlavorType.Spicy, 3);
        system.Resolve(pot, MakeIngredient(0, (FlavorType.Numbing, 1)));

        Assert(verb.ApplyCount == 3, $"共振应有限触发（封顶 3），实际 {verb.ApplyCount}");
        Assert(pot.GetFlavor(FlavorType.Numbing) == 3, "每次触发独立 +1 麻，且不再递归共振");
    }

    static void Test_Numbing_NotRegisteredAsNormalVerb()
    {
        Assert(!FlavorInteractionSystem.Default.Verbs.ContainsKey(FlavorType.Numbing),
            "麻不应注册普通味道动词（共振由互动层独立步骤执行）");
    }

    // ── 7. 预览一致 ──────────────────────────────────────────────────────────

    static void VerifyPreviewMatchesReal(
        Action<GameState> setup, Func<IngredientInstance> makeAdded, FlavorConfig? config)
    {
        var previewCtrl = MakeControllerAtIngredientResolve(out var previewState, config: config);
        setup(previewState);
        var preview = previewCtrl.PreviewIngredient(makeAdded(), new EffectSystem());

        var realCtrl = MakeControllerAtIngredientResolve(out var realState, config: config);
        setup(realState);
        realCtrl.AddIngredient(makeAdded(), new EffectSystem());
        realCtrl.AdvanceBowlPhase(); // → ScoreCalculation
        realCtrl.CalculateScore();

        Assert(preview.PreviewBaseScore == realState.Pot.BaseScore,
            $"预览 BaseScore({preview.PreviewBaseScore}) 应与真实结算({realState.Pot.BaseScore}) 一致");
        Assert(preview.PreviewFinalScore == realState.Pot.FinalScore,
            $"预览 FinalScore({preview.PreviewFinalScore}) 应与真实结算({realState.Pot.FinalScore}) 一致");
    }

    static void Test_Preview_MatchesReal_Aging()
    {
        // 陈酿立即到期：池 10 + 本次存入 5 = 15 → floor 15 并入基础分。
        var config = new FlavorConfig { AgingMatureAdds = 1 };
        VerifyPreviewMatchesReal(
            state => { state.Pot.AgingPool = 10; state.Pot.AgingAdds = 1; },
            () => MakeIngredient(10, (FlavorType.Bitter, 1)),
            config);
    }

    static void Test_Preview_MatchesReal_Heat()
    {
        VerifyPreviewMatchesReal(
            state => state.Pot.AddFlavor(FlavorType.Sweet, 2),
            () => MakeIngredient(3, (FlavorType.Spicy, 1)),
            config: null);
    }

    static void Test_Preview_MatchesReal_Umami()
    {
        VerifyPreviewMatchesReal(
            state => { state.Pot.AddFlavor(FlavorType.Umami, 1); state.Pot.AddFlavor(FlavorType.Sweet, 2); },
            () => MakeIngredient(1, (FlavorType.Umami, 1)),
            config: null);
    }

    // ── 8. 快照深拷贝新状态 ──────────────────────────────────────────────────

    static void Test_Preview_SnapshotDeepCopiesF3State()
    {
        var config = new FlavorConfig { AgingMatureAdds = 3 };
        var ctrl = MakeControllerAtIngredientResolve(out var state, config: config);
        state.Pot.AgingPool = 4;
        state.Pot.AgingAdds = 2;         // 本次预览加料后达到 3 次 → 到期兑现 floor(4×1.1)=4
        state.Pot.IsSolidified = true;
        state.Pot.HeatBowlsRemaining = 1;
        state.Pot.HeatBonusTiers = 1;    // 第1碗 ×1 + 1 档 = ×2

        var preview = ctrl.PreviewIngredient(MakeIngredient(2), new EffectSystem());

        // 若快照未拷贝陈酿池 / 余温状态，以下数值将漂移。
        Assert(preview.PreviewBaseScore == 6, "快照应带入陈酿池：2(基础) + 4(到期兑现) = 6");
        Assert(preview.PreviewFinalScore == 12, "快照应带入余温档位：floor(6 × 2) = 12");

        // 预览不得改动真实状态。
        Assert(state.Pot.AgingPool == 4, "预览不得修改真实陈酿池");
        Assert(state.Pot.AgingAdds == 2, "预览不得修改真实陈酿计数");
        Assert(state.Pot.IsSolidified, "预览不得清除真实固化状态");
        Assert(state.Pot.HeatBowlsRemaining == 1 && state.Pot.HeatBonusTiers == 1,
            "预览不得修改真实余温状态");
        Assert(state.Pot.UmamiMultiplier == 1.0, "预览不得修改真实提鲜系数");
        Assert(state.Pot.BaseScore == 0, "预览不得修改真实基础分");
    }

    // ─────────────────────────────────────────────────────────────────────────

    static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception($"[FAIL] ComplexVerbTests: {message}");
    }

    static void AssertClose(double actual, double expected, string message)
    {
        if (Math.Abs(actual - expected) > 1e-9)
            throw new Exception($"[FAIL] ComplexVerbTests: {message}（期望 {expected}，实际 {actual}）");
    }
}
