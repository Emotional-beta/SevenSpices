using SevenSpices.Core.Content;
using SevenSpices.Core.Effects;
using SevenSpices.Core.Flavors;
using SevenSpices.Core.Game;
using SevenSpices.Core.Ingredients;
using SevenSpices.Core.Pot;
using SevenSpices.Core.Scoring;

namespace SevenSpices.Tests.Flavors;

/// <summary>
/// 味道系统 F4 测试：味道熵（双轴 + 纯味轻惩罚）。
/// 覆盖丰盛倍率（含封顶）、寡淡惩罚、动词超频（甜·复制 / 苦·陈酿）、
/// 派生只读边界、以及预览与结算一致。
/// </summary>
public static class FlavorEntropyTests
{
    public static void RunAll()
    {
        Test_Abundance_ByTypeCount_AndCap();
        Test_AbundanceRequirement_DataDrivenAndDefensive();
        Test_Bland_OnlyOneType_Penalty();
        Test_Specialization_SweetDuplicate_Potency();
        Test_Specialization_BitterAging_Potency();
        Test_Derived_TypeCount_And_PotencyBoundaries();
        Test_Preview_IncludesAbundanceAndBland();

        Console.WriteLine("All FlavorEntropyTests passed.");
    }

    // ── 辅助 ─────────────────────────────────────────────────────────────────

    static PotController MakeControllerAtIngredientResolve(out GameState state, FlavorConfig? config = null)
    {
        state = new GameState();
        var ctrl = new PotController(state, flavorConfig: config);
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

    // ── 1. 丰盛倍率 ───────────────────────────────────────────────────────────

    static void Test_Abundance_ByTypeCount_AndCap()
    {
        // 0 / 1 种 → 系数 1（无加成）。
        var empty = new PotState();
        AssertClose(ScoreCalculator.GetAbundanceMultiplier(empty), 1.0, "0 种味道 → 丰盛 1.0");

        var one = new PotState();
        one.AddFlavor(FlavorType.Sweet, 3);
        AssertClose(ScoreCalculator.GetAbundanceMultiplier(one), 1.0, "1 种味道 → 丰盛 1.0");

        // 默认 0.1/种：2 → 1.1；3 → 1.2；7 → 1.6。
        var two = new PotState();
        two.AddFlavor(FlavorType.Sweet, 1);
        two.AddFlavor(FlavorType.Salty, 1);
        AssertClose(ScoreCalculator.GetAbundanceMultiplier(two), 1.1, "2 种味道 → 1.1");

        var three = new PotState();
        three.AddFlavor(FlavorType.Sweet, 1);
        three.AddFlavor(FlavorType.Salty, 1);
        three.AddFlavor(FlavorType.Sour, 1);
        AssertClose(ScoreCalculator.GetAbundanceMultiplier(three), 1.2, "3 种味道 → 1.2");

        var seven = new PotState();
        foreach (FlavorType flavor in Enum.GetValues<FlavorType>())
            seven.AddFlavor(flavor, 1);
        AssertClose(ScoreCalculator.GetAbundanceMultiplier(seven), 1.6, "7 种味道 → 1 + 0.1×6 = 1.6");

        // 封顶：0.5/种、封顶 2.0 → 3 种即达 2.0；更多种类不再增长。
        var capConfig = new FlavorConfig { AbundancePerType = 0.5, AbundanceMaxMultiplier = 2.0 };
        var cap3 = new PotState { Config = capConfig };
        cap3.AddFlavor(FlavorType.Sweet, 1);
        cap3.AddFlavor(FlavorType.Salty, 1);
        cap3.AddFlavor(FlavorType.Sour, 1);
        AssertClose(ScoreCalculator.GetAbundanceMultiplier(cap3), 2.0, "3 种 ×0.5 应达封顶 2.0");

        var cap5 = new PotState { Config = capConfig };
        cap5.AddFlavor(FlavorType.Sweet, 1);
        cap5.AddFlavor(FlavorType.Salty, 1);
        cap5.AddFlavor(FlavorType.Sour, 1);
        cap5.AddFlavor(FlavorType.Bitter, 1);
        cap5.AddFlavor(FlavorType.Spicy, 1);
        AssertClose(ScoreCalculator.GetAbundanceMultiplier(cap5), 2.0, "超过封顶后不再增长，仍为 2.0");

        // 纳入最终分：BaseScore=8 + 味道 2 = 10，2 种 → floor(10×1.1) = 11。
        var pot = new PotState { BowlNumber = 1, BaseScore = 8 };
        pot.AddFlavor(FlavorType.Sweet, 1);
        pot.AddFlavor(FlavorType.Salty, 1);
        Assert(ScoreCalculator.ComputeFinalScore(pot) == 11,
            "丰盛倍率应纳入最终分：floor((8+2)×1.1) = 11");
    }

    /// <summary>丰盛门槛已数据驱动：默认取自 FlavorConfig，注入的自定义门槛生效，且 0/负数有下限防御。</summary>
    static void Test_AbundanceRequirement_DataDrivenAndDefensive()
    {
        // 默认：PotState 门槛取自 FlavorConfig.Default。
        Assert(new PotState().AbundanceFlavorTypeRequirement
               == FlavorConfig.Default.AbundanceFlavorTypeRequirement,
            "PotState 默认丰盛门槛应取自 FlavorConfig.Default");

        // 数据驱动：注入自定义门槛后，开锅时真正注入 PotState。
        MakeControllerAtIngredientResolve(out var state,
            new FlavorConfig { AbundanceFlavorTypeRequirement = 3 });
        Assert(state.Pot.AbundanceFlavorTypeRequirement == 3,
            $"自定义配置门槛应为 3，实际 {state.Pot.AbundanceFlavorTypeRequirement}");

        // 防御 0 / 负数：空锅绝不误判丰盛。
        var zero = new PotState { AbundanceFlavorTypeRequirement = 0 };
        AssertClose(ScoreCalculator.GetAbundanceMultiplier(zero), 1.0, "门槛 0 且空锅 → 丰盛 1.0");

        var negative = new PotState { AbundanceFlavorTypeRequirement = -5 };
        AssertClose(ScoreCalculator.GetAbundanceMultiplier(negative), 1.0, "门槛负数且空锅 → 丰盛 1.0");

        // 门槛 0 且有 1 种味道：钳制为 1 → 1 + 0.1×1 = 1.1。
        var zeroOne = new PotState { AbundanceFlavorTypeRequirement = 0 };
        zeroOne.AddFlavor(FlavorType.Sweet, 1);
        AssertClose(ScoreCalculator.GetAbundanceMultiplier(zeroOne), 1.1,
            "门槛 0 钳制为 1 → 1 种味道即 1.1");
    }

    // ── 2. 寡淡惩罚 ───────────────────────────────────────────────────────────

    static void Test_Bland_OnlyOneType_Penalty()
    {
        // 仅 1 种：×0.9。BaseScoreWithFlavor = 10 + 1 = 11 → floor(11×0.9) = 9。
        var one = new PotState { BowlNumber = 1, BaseScore = 10 };
        one.AddFlavor(FlavorType.Sweet, 1);
        AssertClose(ScoreCalculator.GetBlandPenalty(one), 0.9, "1 种味道 → 寡淡系数 0.9");
        Assert(ScoreCalculator.ComputeFinalScore(one) == 9,
            "寡淡应纳入最终分：floor((10+1)×0.9) = 9");

        // 0 种不惩罚。
        var none = new PotState { BowlNumber = 1, BaseScore = 10 };
        AssertClose(ScoreCalculator.GetBlandPenalty(none), 1.0, "0 种味道不触发寡淡");
        Assert(ScoreCalculator.ComputeFinalScore(none) == 10, "0 种味道 → 最终分 10（无惩罚）");

        // 2 种不惩罚（只剩丰盛加成）。
        var two = new PotState { BowlNumber = 1, BaseScore = 8 };
        two.AddFlavor(FlavorType.Sweet, 1);
        two.AddFlavor(FlavorType.Salty, 1);
        AssertClose(ScoreCalculator.GetBlandPenalty(two), 1.0, "2 种味道不触发寡淡");
        Assert(ScoreCalculator.ComputeFinalScore(two) == 11,
            "2 种味道：floor((8+2)×1.1) = 11（无寡淡惩罚）");
    }

    // ── 3. 精·超频：甜·复制 ───────────────────────────────────────────────────

    static void Test_Specialization_SweetDuplicate_Potency()
    {
        var config = new FlavorConfig(); // SpecializationStep=5，MaxPotency=3

        // 甜=5 → potency 1 → 复制 +1 → 6。
        var pot5 = new PotState { Config = config };
        pot5.AddFlavor(FlavorType.Sweet, 5);
        FlavorInteractionSystem.Default.Resolve(pot5, MakeIngredient(0, (FlavorType.Sweet, 1)));
        Assert(pot5.GetFlavor(FlavorType.Sweet) == 6, "甜=5（potency 1）→ 复制 +1 = 6");

        // 甜=6 → potency 2 → 复制 +2 → 8。
        var pot6 = new PotState { Config = config };
        pot6.AddFlavor(FlavorType.Sweet, 6);
        FlavorInteractionSystem.Default.Resolve(pot6, MakeIngredient(0, (FlavorType.Sweet, 1)));
        Assert(pot6.GetFlavor(FlavorType.Sweet) == 8, "甜=6（potency 2）→ 复制 +2 = 8");

        // 甜=11 → potency 3 → 复制 +3 → 14。
        var pot11 = new PotState { Config = config };
        pot11.AddFlavor(FlavorType.Sweet, 11);
        FlavorInteractionSystem.Default.Resolve(pot11, MakeIngredient(0, (FlavorType.Sweet, 1)));
        Assert(pot11.GetFlavor(FlavorType.Sweet) == 14, "甜=11（potency 3）→ 复制 +3 = 14");
    }

    // ── 4. 精·超频：苦·陈酿 ───────────────────────────────────────────────────

    static void Test_Specialization_BitterAging_Potency()
    {
        var config = new FlavorConfig(); // 存入 0.5、复利 0.1、到期 3

        // 苦=5 → potency 1：存入 10×0.5×1 = 5 → 当次复利 ×1.1 = 5.5
        var pot5 = new PotState { Config = config };
        pot5.AddFlavor(FlavorType.Bitter, 5);
        FlavorInteractionSystem.Default.Resolve(
            pot5, MakeIngredient(0, (FlavorType.Bitter, 1)), baseScoreAdded: 10);
        AssertClose(pot5.AgingPool, 5.5, "苦=5（potency 1）→ 10×0.5×1.1 = 5.5");

        // 苦=6 → potency 2：存入 10×0.5×2 = 10 → 当次复利 ×1.1 = 11
        var pot6 = new PotState { Config = config };
        pot6.AddFlavor(FlavorType.Bitter, 6);
        FlavorInteractionSystem.Default.Resolve(
            pot6, MakeIngredient(0, (FlavorType.Bitter, 1)), baseScoreAdded: 10);
        AssertClose(pot6.AgingPool, 11.0, "苦=6（potency 2）→ 10×0.5×2×1.1 = 11");
    }

    // ── 5. 派生只读边界 ───────────────────────────────────────────────────────

    static void Test_Derived_TypeCount_And_PotencyBoundaries()
    {
        // ActiveFlavorTypeCount：只统计值 > 0 的味道。
        var pot = new PotState();
        Assert(pot.ActiveFlavorTypeCount == 0, "空锅味道种类应为 0");
        pot.AddFlavor(FlavorType.Sour, 1);
        Assert(pot.ActiveFlavorTypeCount == 1, "加酸后种类应为 1");
        pot.AddFlavor(FlavorType.Salty, 2);
        Assert(pot.ActiveFlavorTypeCount == 2, "加咸后种类应为 2");
        pot.AddFlavor(FlavorType.Sour, -1);
        Assert(pot.ActiveFlavorTypeCount == 1, "味道值降为 0 后不应计入种类数");

        // GetVerbPotency 边界：步长 5、封顶 3。
        var p = new PotState();
        Assert(p.GetVerbPotency(FlavorType.Sweet) == 1, "值 0 → potency 1");
        p.AddFlavor(FlavorType.Sweet, 1);
        Assert(p.GetVerbPotency(FlavorType.Sweet) == 1, "值 1 → potency 1");
        p.AddFlavor(FlavorType.Sweet, 4); // 5
        Assert(p.GetVerbPotency(FlavorType.Sweet) == 1, "值 5（差 1 档）→ potency 1");
        p.AddFlavor(FlavorType.Sweet, 1); // 6
        Assert(p.GetVerbPotency(FlavorType.Sweet) == 2, "值 6 → potency 2");
        p.AddFlavor(FlavorType.Sweet, 4); // 10
        Assert(p.GetVerbPotency(FlavorType.Sweet) == 2, "值 10（差 1 档）→ potency 2");
        p.AddFlavor(FlavorType.Sweet, 1); // 11
        Assert(p.GetVerbPotency(FlavorType.Sweet) == 3, "值 11 → potency 3");
        p.AddFlavor(FlavorType.Sweet, 50); // 61
        Assert(p.GetVerbPotency(FlavorType.Sweet) == 3, "远超档位应封顶于 3");

        // 自定义步长 / 封顶。
        var custom = new PotState
        {
            Config = new FlavorConfig { SpecializationStep = 2, SpecializationMaxPotency = 4 }
        };
        custom.AddFlavor(FlavorType.Sweet, 7); // 1 + 6/2 = 4
        Assert(custom.GetVerbPotency(FlavorType.Sweet) == 4, "步长 2：值 7 → potency 4");
        custom.AddFlavor(FlavorType.Sweet, 2); // 9 → 1 + 8/2 = 5 → 封顶 4
        Assert(custom.GetVerbPotency(FlavorType.Sweet) == 4, "步长 2：值 9 应封顶于 4");

        // 防御：步长非法 → 恒为 1。
        var bad = new PotState { Config = new FlavorConfig { SpecializationStep = 0 } };
        bad.AddFlavor(FlavorType.Sweet, 99);
        Assert(bad.GetVerbPotency(FlavorType.Sweet) == 1, "步长 ≤ 0 时 potency 应退化为 1");
    }

    // ── 6. 预览一致 ───────────────────────────────────────────────────────────

    static void Test_Preview_IncludesAbundanceAndBland()
    {
        var es = new EffectSystem();

        // 仅 1 种味道：寡淡 → floor((10+1)×0.9) = 9。
        var ctrl1 = MakeControllerAtIngredientResolve(out _);
        var preview1 = ctrl1.PreviewIngredient(
            MakeIngredient(10, (FlavorType.Salty, 1)), es);
        Assert(preview1.PreviewFinalScore == 9,
            $"仅 1 种味道的预览应含寡淡：floor(11×0.9)=9，实际 {preview1.PreviewFinalScore}");

        // 2 种味道：丰盛 → floor((10+2)×1.1) = 13。
        var ctrl2 = MakeControllerAtIngredientResolve(out var state2);
        state2.Pot.AddFlavor(FlavorType.Sweet, 1);
        var preview2 = ctrl2.PreviewIngredient(
            MakeIngredient(10, (FlavorType.Salty, 1)), es);
        Assert(preview2.PreviewFinalScore == 13,
            $"2 种味道的预览应含丰盛：floor(12×1.1)=13，实际 {preview2.PreviewFinalScore}");

        // 预览应与真实结算一致（丰盛 / 寡淡同一公式）。
        var realCtrl = MakeControllerAtIngredientResolve(out var realState);
        realState.Pot.AddFlavor(FlavorType.Sweet, 1);
        realCtrl.AddIngredient(MakeIngredient(10, (FlavorType.Salty, 1)), es);
        realCtrl.AdvanceBowlPhase(); // → ScoreCalculation
        realCtrl.CalculateScore();
        Assert(preview2.PreviewFinalScore == realState.Pot.FinalScore,
            $"含丰盛 / 寡淡的预览({preview2.PreviewFinalScore}) 应与结算({realState.Pot.FinalScore}) 一致");
    }

    // ─────────────────────────────────────────────────────────────────────────

    static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception($"[FAIL] FlavorEntropyTests: {message}");
    }

    static void AssertClose(double actual, double expected, string message)
    {
        if (Math.Abs(actual - expected) > 1e-9)
            throw new Exception($"[FAIL] FlavorEntropyTests: {message}（期望 {expected}，实际 {actual}）");
    }
}
