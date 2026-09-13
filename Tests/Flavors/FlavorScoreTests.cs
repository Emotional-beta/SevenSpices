using SevenSpices.Core.Content;
using SevenSpices.Core.Effects;
using SevenSpices.Core.Game;
using SevenSpices.Core.Ingredients;
using SevenSpices.Core.Pot;
using SevenSpices.Core.Scoring;

namespace SevenSpices.Tests.Flavors;

/// <summary>
/// 味道系统 F1 地基测试：FlavorType 枚举、味道分、可调权重、算分与预览接入、Reset。
/// </summary>
public static class FlavorScoreTests
{
    public static void RunAll()
    {
        Test_Enum_OrderAndMembers();
        Test_Rice_FlavorScoreAndBaseScore();
        Test_FlavorWeight_Adjustable();
        Test_FlavorWeight_UsesInjectedConfigDefault();
        Test_ComputeFinalScore_IncludesFlavorScore();
        Test_PreviewFinalScore_IncludesFlavorScore();
        Test_Reset_ClearsFlavorsAndWeights();
        Test_SetFlavorWeight_ClampsBelowZero();
        Test_Preview_SnapshotDeepCopiesWeights();

        Console.WriteLine("All FlavorScoreTests passed.");
    }

    // ── 辅助 ─────────────────────────────────────────────────────────────────

    static PotController MakeControllerAtIngredientResolve(out GameState state)
    {
        state = new GameState();
        var ctrl = new PotController(state);
        ctrl.StartPot();
        ctrl.StartBowl();
        ctrl.AdvanceBowlPhase(); // → Customer
        ctrl.AdvanceBowlPhase(); // → ItemPhase
        ctrl.AdvanceBowlPhase(); // → IngredientSelection
        ctrl.AdvanceBowlPhase(); // → IngredientResolve
        return ctrl;
    }

    // ── 测试 ─────────────────────────────────────────────────────────────────

    static void Test_Enum_OrderAndMembers()
    {
        var expected = new[]
        {
            FlavorType.Sour, FlavorType.Sweet, FlavorType.Bitter, FlavorType.Spicy,
            FlavorType.Umami, FlavorType.Salty, FlavorType.Numbing
        };
        var actual = Enum.GetValues<FlavorType>();

        Assert(actual.Length == 7, "味道枚举应为 7 种");
        Assert(actual.SequenceEqual(expected),
            "味道枚举顺序应为 Sour,Sweet,Bitter,Spicy,Umami,Salty,Numbing");
    }

    static void Test_Rice_FlavorScoreAndBaseScore()
    {
        var ctrl = MakeControllerAtIngredientResolve(out var state);
        var es = new EffectSystem();

        ctrl.AddIngredient(IngredientData.CreateInstance("rice"), es);

        Assert(state.Pot.BaseScore == 1, "米饭入锅后 BaseScore 应为 1");
        Assert(state.Pot.FlavorScore == 1, "米饭鲜+1 → FlavorScore 应为 1");
        Assert(state.Pot.BaseScoreWithFlavor == 2, "含味道分的基础分应为 1 + 1 = 2");
    }

    static void Test_FlavorWeight_Adjustable()
    {
        var pot = new PotState();
        pot.AddFlavor(FlavorType.Umami, 1);

        Assert(pot.FlavorScore == 1, "默认权重 1.0 → FlavorScore 应为 1");

        pot.SetFlavorWeight(FlavorType.Umami, 2.0);

        Assert(pot.FlavorScore == 2, "权重提升到 2.0 → FlavorScore 应为 2");
        Assert(pot.GetFlavorWeight(FlavorType.Sweet) == FlavorConfig.Default.DefaultFlavorWeight,
            "未设置权重的味道应返回默认权重");
    }

    /// <summary>未显式设权重的味道必须按「注入本锅的 Config.DefaultFlavorWeight」计算，而非静态默认。</summary>
    static void Test_FlavorWeight_UsesInjectedConfigDefault()
    {
        var config = new FlavorConfig { DefaultFlavorWeight = 2.5 };
        var pot = new PotState { Config = config };
        pot.AddFlavor(FlavorType.Sweet, 3);

        Assert(pot.GetFlavorWeight(FlavorType.Sweet) == 2.5,
            "未设置权重的味道应返回注入配置的 DefaultFlavorWeight");
        Assert(pot.FlavorScore == 7.5,
            "甜 3 × 注入默认权重 2.5 → FlavorScore 应为 7.5");

        pot.SetFlavorWeight(FlavorType.Sweet, 1.0);
        Assert(pot.GetFlavorWeight(FlavorType.Sweet) == 1.0,
            "显式设置的权重应覆盖注入配置的默认权重");
    }

    static void Test_ComputeFinalScore_IncludesFlavorScore()
    {
        var pot = new PotState { BowlNumber = 1, BaseScore = 1 };
        pot.AddFlavor(FlavorType.Umami, 1);

        Assert(ScoreCalculator.ComputeFinalScore(pot) == 1,
            "BaseScore=1、味道分=1、第1碗×1；但仅鲜 1 种 → F4 寡淡 ×0.9 → floor(2×0.9) = 1");
    }

    static void Test_PreviewFinalScore_IncludesFlavorScore()
    {
        var ctrl = MakeControllerAtIngredientResolve(out var state);
        state.Pot.BowlNumber = 6; // ×2
        var es = new EffectSystem();

        var preview = ctrl.PreviewIngredient(IngredientData.CreateInstance("rice"), es);

        Assert(preview.PreviewBaseScore == 1, "米饭预览 BaseScore 应为 1");
        Assert(preview.PreviewFinalScore == 3,
            "第6碗（×2）米饭：仅鲜 1 种 → F4 寡淡 ×0.9 → floor((基础分1 + 味道分1) × 0.9 × 2) = 3");
    }

    static void Test_Reset_ClearsFlavorsAndWeights()
    {
        var pot = new PotState();
        pot.AddFlavor(FlavorType.Sweet, 3);
        pot.SetFlavorWeight(FlavorType.Sweet, 5.0);
        pot.BaseScore = 7;

        pot.Reset();

        Assert(pot.Flavors.Count == 0, "Reset 应清空 Flavors");
        Assert(pot.FlavorWeights.Count == 0, "Reset 应清空 FlavorWeights");
        Assert(pot.FlavorScore == 0, "Reset 后 FlavorScore 应为 0");
    }

    static void Test_SetFlavorWeight_ClampsBelowZero()
    {
        var pot = new PotState();
        pot.AddFlavor(FlavorType.Sour, 5);
        pot.SetFlavorWeight(FlavorType.Sour, -3.0);

        Assert(pot.GetFlavorWeight(FlavorType.Sour) == 0, "负权重应被夹到 0");
        Assert(pot.FlavorScore == 0, "权重为 0 时该味道不计入味道分");
    }

    static void Test_Preview_SnapshotDeepCopiesWeights()
    {
        var ctrl = MakeControllerAtIngredientResolve(out var state);
        // 用「咸」验证权重快照：咸·固化不消耗咸值，数值在动词结算后仍守恒。
        state.Pot.AddFlavor(FlavorType.Salty, 2);
        state.Pot.SetFlavorWeight(FlavorType.Salty, 3.0);
        var def = new IngredientDefinition(
            id: "salty_filler", name: "测试咸味", rarity: IngredientRarity.Common,
            baseScore: 2, flavors: new() { [FlavorType.Salty] = 1 });
        var inst = new IngredientInstance(def);
        var es = new EffectSystem();

        var preview = ctrl.PreviewIngredient(inst, es);

        // 咸：2 + 1 = 3，权重 3.0 → 味道分 9；基础分 2 → 合计 11；仅咸 1 种 → F4 寡淡 ×0.9 → floor(11×0.9)=9
        Assert(preview.PreviewFinalScore == 9,
            "预览应沿用快照中的味道权重，并应用 F4 寡淡：咸3 × 权重3 = 9，加基础分2 → 11，×0.9 → 9");
        Assert(state.Pot.FlavorWeights[FlavorType.Salty] == 3.0, "预览不得修改真实权重");
    }

    static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception($"[FAIL] FlavorScoreTests: {message}");
    }
}
