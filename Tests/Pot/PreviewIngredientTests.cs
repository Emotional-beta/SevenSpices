using SevenSpices.Core.Content;
using SevenSpices.Core.Effects;
using SevenSpices.Core.Game;
using SevenSpices.Core.Ingredients;
using SevenSpices.Core.Pot;
using SevenSpices.Core.Run;
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
        Test_Preview_FinalScore_Bowl1_IncludesFlavorScore();
        Test_Preview_FinalScore_Bowl6_AppliesMultiplier();
        Test_Preview_FinalScore_Bowl10_AppliesMultiplier();
        Test_Preview_FinalScore_WithIceCube_IncludesEffectMultiplier();
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
        Test_Preview_AtIngredientSelection_ReturnsAndDoesNotMutateState();
        Test_Preview_WrongPhase_Throws();
        Test_Preview_FinalPot_RealizesAgingPool_MatchesEndCooking();
        Test_Preview_NormalPot_DoesNotRealizeAgingPool();

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

    static PotController MakeControllerAtIngredientSelection(out GameState state)
    {
        state = new GameState();
        var ctrl = new PotController(state);
        ctrl.StartPot();
        ctrl.StartBowl();
        ctrl.AdvanceBowlPhase(); // → Customer
        ctrl.AdvanceBowlPhase(); // → ItemPhase
        ctrl.AdvanceBowlPhase(); // → IngredientSelection
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

    // ── A2. 最终分预测（应用碗倍率与效果倍率） ────────────────────────────────

    static void Test_Preview_FinalScore_Bowl1_IncludesFlavorScore()
    {
        var ctrl = MakeControllerAtIngredientResolve(out var state);
        state.Pot.BowlNumber = 1; // ×1
        var inst = IngredientData.CreateInstance("rice"); // BaseScore=1，鲜+1
        var es = new EffectSystem();

        var preview = ctrl.PreviewIngredient(inst, es);

        Assert(preview.PreviewBaseScore == 1, "第1碗米饭预测 BaseScore 应为 1");
        Assert(preview.PreviewFinalScore == 1,
            "第1碗（×1）米饭含味道分，但仅鲜 1 种 → F4 寡淡 ×0.9：floor((1+1)×0.9×1) = 1");
    }

    static void Test_Preview_FinalScore_Bowl6_AppliesMultiplier()
    {
        var ctrl = MakeControllerAtIngredientResolve(out var state);
        state.Pot.BowlNumber = 6; // ×2
        var inst = IngredientData.CreateInstance("rice"); // BaseScore=1，鲜+1（提鲜不改鲜值）
        var es = new EffectSystem();

        var preview = ctrl.PreviewIngredient(inst, es);

        Assert(preview.PreviewBaseScore == 1, "第6碗米饭预测 BaseScore 应为 1");
        Assert(preview.PreviewFinalScore == 3,
            "第6碗（×2）米饭仅鲜 1 种 → F4 寡淡 ×0.9：floor((1+1)×0.9×2) = 3");
    }

    static void Test_Preview_FinalScore_Bowl10_AppliesMultiplier()
    {
        var ctrl = MakeControllerAtIngredientResolve(out var state);
        state.Pot.BowlNumber = 10; // ×32
        var inst = IngredientData.CreateInstance("rice"); // BaseScore=1，鲜+1
        var es = new EffectSystem();

        var preview = ctrl.PreviewIngredient(inst, es);

        Assert(preview.PreviewBaseScore == 1, "第10碗米饭预测 BaseScore 应为 1");
        Assert(preview.PreviewFinalScore == 57,
            "第10碗（×32）米饭仅鲜 1 种 → F4 寡淡 ×0.9：floor((1+1)×0.9×32) = 57");
    }

    static void Test_Preview_FinalScore_WithIceCube_IncludesEffectMultiplier()
    {
        var ctrl = MakeControllerAtIngredientResolve(out var state);
        state.Pot.BowlNumber = 1; // ×1
        state.Pot.BaseScore = 2;  // 已有基础分，冰块 BaseScore=0，仅贡献 ×1.5 倍率
        var inst = IngredientData.CreateInstance("ice_cube");
        var es = new EffectSystem();

        var preview = ctrl.PreviewIngredient(inst, es);

        Assert(preview.PreviewBaseScore == 2, "冰块不增加基础分，预测 BaseScore 应保持 2");
        Assert(preview.PreviewFinalScore == 3,
            "第1碗（×1）含冰块效果倍率 1.5 时，floor(2 × 1 × 1.5) = 3");
    }

    // ── B. 味道效果预测 ───────────────────────────────────────────────────────

    static void Test_Preview_WithFlavor_ConditionalEffect_NotYetMet()
    {
        var ctrl = MakeControllerAtIngredientResolve(out _);
        // 当前甜=0，红枣加甜+1 后经甜·复制为 2，不满足甜≥3，不触发+2
        var inst = IngredientData.CreateInstance("red_date"); // BaseScore=1, Sweet+1, 甜≥3→+2
        var es = new EffectSystem();

        var preview = ctrl.PreviewIngredient(inst, es);

        // BaseScore=0+1(基础分)=1，甜=0+1+1(复制)=2，不触发 ConditionalEffect
        Assert(preview.PreviewBaseScore == 1, "甜<3时红枣预测 BaseScore 应为 1（不含条件效果）");
    }

    static void Test_Preview_WithFlavor_ConditionalEffect_Met()
    {
        var ctrl = MakeControllerAtIngredientResolve(out var state);
        // 当前甜=1，红枣加甜+1 后经甜·复制为 3，满足甜≥3，触发+2
        state.Pot.AddFlavor(FlavorType.Sweet, 1);
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

    /// <summary>IngredientSelection 阶段（A2 悬停候选场景）预览：正常返回且不修改真实状态。</summary>
    static void Test_Preview_AtIngredientSelection_ReturnsAndDoesNotMutateState()
    {
        var ctrl = MakeControllerAtIngredientSelection(out var state);
        var inst = IngredientData.CreateInstance("rice"); // BaseScore=1, Umami+1
        var es = new EffectSystem();

        int baseBefore = state.Pot.BaseScore;
        int totalBefore = state.Pot.TotalBaseScore;
        int umamiBefore = state.Pot.GetFlavor(FlavorType.Umami);
        var phaseBefore = state.Pot.CurrentBowlPhase;

        var preview = ctrl.PreviewIngredient(inst, es);

        Assert(preview.PreviewBaseScore == baseBefore + inst.Definition.BaseScore,
            "IngredientSelection 阶段预览应返回正确基础分");
        Assert(state.Pot.BaseScore == baseBefore, "IngredientSelection 阶段预览不得修改真实 BaseScore");
        Assert(state.Pot.TotalBaseScore == totalBefore,
            "IngredientSelection 阶段预览不得修改 TotalBaseScore");
        Assert(state.Pot.GetFlavor(FlavorType.Umami) == umamiBefore,
            "IngredientSelection 阶段预览不得修改真实味道");
        Assert(state.Pot.Ingredients.Count == 0,
            "IngredientSelection 阶段预览不得向锅中添加食材");
        Assert(state.Pot.CurrentBowlPhase == phaseBefore,
            "IngredientSelection 阶段预览不得修改碗阶段");
    }

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

        Assert(threw, "非 IngredientSelection / IngredientResolve 阶段调用 PreviewIngredient 应抛 InvalidOperationException");
    }

    // ── E. 最终锅陈酿池兑现（Bug 4 回归） ──────────────────────────────────────

    /// <summary>
    /// 最终锅带非零陈酿池时，悬停预览应兑现陈酿池，且预测最终分与随后真实 EndCooking 一致。
    /// </summary>
    static void Test_Preview_FinalPot_RealizesAgingPool_MatchesEndCooking()
    {
        var state = new GameState();
        var gc = new GameController(state);
        gc.StartNewGame();

        // 直达最终锅。
        gc.Run.Chapter = RunController.ChaptersPerRun;
        gc.Run.PotIndex = RunController.PotsPerChapter;
        gc.Run.IsFinalPot = true;
        gc.StartCurrentPot();

        state.Pot.AgingPool = 5.0;

        var candidate = gc.CurrentCandidates[0];
        var preview = gc.PreviewIngredient(candidate);

        // 真实路径：投入同一候选 → EndCooking（内部先 RealizeAgingPool 再算分）。
        gc.SelectIngredient(candidate.InstanceId);
        gc.EndCooking();

        Assert(preview.PreviewFinalScore == state.Pot.FinalScore,
            $"最终锅预览应兑现陈酿池并与真实结算一致：预览 {preview.PreviewFinalScore}，实际 {state.Pot.FinalScore}");
    }

    /// <summary>普通锅陈酿只在到期时兑现，预览不得无条件兑现（否则预览分虚高）。</summary>
    static void Test_Preview_NormalPot_DoesNotRealizeAgingPool()
    {
        var ctrl = MakeControllerAtIngredientResolve(out var state);
        state.Pot.AgingPool = 5.0;
        var inst = IngredientData.CreateInstance("rice"); // BaseScore 1，鲜+1
        var es = new EffectSystem();

        var preview = ctrl.PreviewIngredient(inst, es);

        Assert(state.Pot.AgingPool == 5.0, "普通锅预览不得兑现真实陈酿池");
        Assert(state.Pot.BaseScore == 0, "普通锅预览不得修改真实 BaseScore");
        Assert(preview.PreviewFinalScore == 1,
            $"普通锅预览不应额外兑现陈酿池：期望 floor((1+1)×0.9×1)=1，实际 {preview.PreviewFinalScore}");
    }

    // ─────────────────────────────────────────────────────────────────────────

    static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception($"[FAIL] PreviewIngredientTests: {message}");
    }
}
