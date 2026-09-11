using SevenSpices.Core.Content;
using SevenSpices.Core.Effects;
using SevenSpices.Core.Game;
using SevenSpices.Core.Ingredients;
using SevenSpices.Core.Pot;
using SevenSpices.Tests.Effects;

namespace SevenSpices.Tests.Pot;

/// <summary>
/// PotController.PreviewIngredient 无副作用预测测试。
/// Phase 5 Part 7。
/// </summary>
public static class PreviewIngredientTests
{
    public static void RunAll()
    {
        Test_Preview_BasicScore();
        Test_Preview_WithFlavor_ConditionalEffect_NotYetMet();
        Test_Preview_WithFlavor_ConditionalEffect_Met();
        Test_Preview_NoSideEffect_BaseScore();
        Test_Preview_NoSideEffect_TotalBaseScore();
        Test_Preview_NoSideEffect_Flavors();
        Test_Preview_NoSideEffect_Ingredients();
        Test_Preview_NoSideEffect_Phase();
        Test_Preview_NoSideEffect_BowlNumber();
        Test_Preview_NoSideEffect_FinalScoreMultiplier();
        Test_Preview_MatchesRealExecution_Rice();
        Test_Preview_MatchesRealExecution_Sugar();
        Test_Preview_MatchesRealExecution_Pepper();
        Test_Preview_MatchesRealExecution_RedDate_ConditionMet();
        Test_Preview_WrongPhase_Throws();

        Console.WriteLine("All PreviewIngredientTests passed.");
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

    // ── A. 基础分预测 ─────────────────────────────────────────────────────────

    static void Test_Preview_BasicScore()
    {
        var ctrl = MakeControllerAtIngredientResolve(out _);
        var inst = IngredientData.CreateInstance("rice"); // BaseScore=1
        var es = new EffectSystem();

        var preview = ctrl.PreviewIngredient(inst, es);

        Assert(preview.PreviewBaseScore == 1, "米饭预测 BaseScore 应为 1");
    }

    // ── B. 味道效果预测 ───────────────────────────────────────────────────────

    static void Test_Preview_WithFlavor_ConditionalEffect_NotYetMet()
    {
        var ctrl = MakeControllerAtIngredientResolve(out var state);
        // 当前甜=1，红枣加甜+1后甜=2，不满足甜≥3，不触发+2
        state.Pot.AddFlavor(FlavorType.Sweet, 1);
        var inst = IngredientData.CreateInstance("red_date"); // BaseScore=1, Sweet+1, 甜≥3→+2
        var es = new EffectSystem();

        var preview = ctrl.PreviewIngredient(inst, es);

        // BaseScore=0+1(基础分)=1，甜=1+1=2，不触发 ConditionalEffect
        Assert(preview.PreviewBaseScore == 1, "甜<3时红枣预测 BaseScore 应为 1（不含条件效果）");
    }

    static void Test_Preview_WithFlavor_ConditionalEffect_Met()
    {
        var ctrl = MakeControllerAtIngredientResolve(out var state);
        // 当前甜=2，红枣加甜+1后甜=3，满足甜≥3，触发+2
        state.Pot.AddFlavor(FlavorType.Sweet, 2);
        var inst = IngredientData.CreateInstance("red_date"); // BaseScore=1, Sweet+1, 甜≥3→+2
        var es = new EffectSystem();

        var preview = ctrl.PreviewIngredient(inst, es);

        // BaseScore=0+1(基础分)+2(条件效果)=3
        Assert(preview.PreviewBaseScore == 3, "甜≥3时红枣预测 BaseScore 应为 3（含条件效果 +2）");
    }

    // ── C. 无副作用验证 ───────────────────────────────────────────────────────

    static void Test_Preview_NoSideEffect_BaseScore()
    {
        var ctrl = MakeControllerAtIngredientResolve(out var state);
        state.Pot.BaseScore = 5;
        var inst = IngredientData.CreateInstance("sugar"); // BaseScore=2
        var es = new EffectSystem();

        ctrl.PreviewIngredient(inst, es);

        Assert(state.Pot.BaseScore == 5, "PreviewIngredient 不得修改真实 BaseScore");
    }

    static void Test_Preview_NoSideEffect_TotalBaseScore()
    {
        var ctrl = MakeControllerAtIngredientResolve(out var state);
        state.Pot.TotalBaseScore = 10;
        var inst = IngredientData.CreateInstance("sugar");
        var es = new EffectSystem();

        ctrl.PreviewIngredient(inst, es);

        Assert(state.Pot.TotalBaseScore == 10, "PreviewIngredient 不得修改 TotalBaseScore");
    }

    static void Test_Preview_NoSideEffect_Flavors()
    {
        var ctrl = MakeControllerAtIngredientResolve(out var state);
        state.Pot.AddFlavor(FlavorType.Sweet, 1);
        var inst = IngredientData.CreateInstance("sugar"); // Sweet+1
        var es = new EffectSystem();

        ctrl.PreviewIngredient(inst, es);

        Assert(state.Pot.GetFlavor(FlavorType.Sweet) == 1, "PreviewIngredient 不得修改真实 Flavors");
    }

    static void Test_Preview_NoSideEffect_Ingredients()
    {
        var ctrl = MakeControllerAtIngredientResolve(out var state);
        var inst = IngredientData.CreateInstance("rice");
        var es = new EffectSystem();

        ctrl.PreviewIngredient(inst, es);

        Assert(state.Pot.Ingredients.Count == 0, "PreviewIngredient 不得向真实 Ingredients 列表添加食材");
    }

    static void Test_Preview_NoSideEffect_Phase()
    {
        var ctrl = MakeControllerAtIngredientResolve(out var state);
        var inst = IngredientData.CreateInstance("rice");
        var es = new EffectSystem();

        ctrl.PreviewIngredient(inst, es);

        Assert(state.Pot.Phase == PotPhase.InProgress, "PreviewIngredient 不得修改 PotPhase");
        Assert(state.Pot.CurrentBowlPhase == BowlPhase.IngredientResolve, "PreviewIngredient 不得修改 BowlPhase");
    }

    static void Test_Preview_NoSideEffect_BowlNumber()
    {
        var ctrl = MakeControllerAtIngredientResolve(out var state);
        var inst = IngredientData.CreateInstance("rice");
        var es = new EffectSystem();

        ctrl.PreviewIngredient(inst, es);

        Assert(state.Pot.BowlNumber == 1, "PreviewIngredient 不得修改 BowlNumber");
    }

    static void Test_Preview_NoSideEffect_FinalScoreMultiplier()
    {
        var ctrl = MakeControllerAtIngredientResolve(out var state);
        var inst = IngredientData.CreateInstance("ice_cube"); // FinalScoreMultiplier × 1.5
        var es = new EffectSystem();

        ctrl.PreviewIngredient(inst, es);

        Assert(state.Pot.FinalScoreMultiplier == 1.0,
            "PreviewIngredient 不得修改真实 FinalScoreMultiplier（冰块效果只在快照生效）");
    }

    // ── D. 预测结果与真实执行一致 ─────────────────────────────────────────────

    static void VerifyPreviewMatchesReal(string ingredientId, string name, Action<GameState>? setup = null)
    {
        // 预测阶段
        var previewCtrl = MakeControllerAtIngredientResolve(out var previewState);
        setup?.Invoke(previewState);
        var previewInst = IngredientData.CreateInstance(ingredientId);
        var previewEs = new EffectSystem();
        var preview = previewCtrl.PreviewIngredient(previewInst, previewEs);

        // 真实执行阶段（相同初始状态）
        var realCtrl = MakeControllerAtIngredientResolve(out var realState);
        setup?.Invoke(realState);
        var realInst = IngredientData.CreateInstance(ingredientId);
        var realEs = new EffectSystem();
        realCtrl.AddIngredient(realInst, realEs);

        Assert(preview.PreviewBaseScore == realState.Pot.BaseScore,
            $"{name}：预测 BaseScore({preview.PreviewBaseScore}) 应等于真实执行后 BaseScore({realState.Pot.BaseScore})");
    }

    static void Test_Preview_MatchesRealExecution_Rice() =>
        VerifyPreviewMatchesReal("rice", "米饭");

    static void Test_Preview_MatchesRealExecution_Sugar() =>
        VerifyPreviewMatchesReal("sugar", "糖");

    static void Test_Preview_MatchesRealExecution_Pepper() =>
        VerifyPreviewMatchesReal("pepper", "辣椒");

    static void Test_Preview_MatchesRealExecution_RedDate_ConditionMet() =>
        VerifyPreviewMatchesReal("red_date", "红枣（甜≥3）",
            state => state.Pot.AddFlavor(FlavorType.Sweet, 2));

    // ── 错误相位保护 ──────────────────────────────────────────────────────────

    static void Test_Preview_WrongPhase_Throws()
    {
        var state = new GameState();
        var ctrl = new PotController(state);
        ctrl.StartPot();
        ctrl.StartBowl(); // BowlPhase.Start
        var inst = IngredientData.CreateInstance("rice");
        var es = new EffectSystem();

        bool threw = false;
        try { ctrl.PreviewIngredient(inst, es); }
        catch (InvalidOperationException) { threw = true; }

        Assert(threw, "非 IngredientResolve 阶段调用 PreviewIngredient 应抛 InvalidOperationException");
    }

    // ─────────────────────────────────────────────────────────────────────────

    static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception($"[FAIL] PreviewIngredientTests: {message}");
    }
}
