using SevenSpices.Core.Content;
using SevenSpices.Core.Game;
using SevenSpices.Core.Ingredients;
using SevenSpices.Core.Pot;
using SevenSpices.Core.Run;
using SevenSpices.Core.Scoring;

namespace SevenSpices.Tests.Game;

/// <summary>
/// GameController（Game Layer 编排）测试。
/// A2：接入食材池抽取（每碗 3 选 1），验证本锅池快照、跨锅重建与选中/跳过流程。
/// </summary>
public static class GameControllerTests
{
    public static void RunAll()
    {
        Test_StartNewGame_InitialState();
        Test_SelectIngredient_AddsToPot_ConsumesPool_KeepsBasket();
        Test_UnselectedCandidates_ReturnToPool_DrawAllWhenPoolBelowThree();
        Test_PoolExhausted_SixSelections_ThenSkipToEnd();
        Test_AdvanceToNextPot_RebuildsPoolFromBasket();
        Test_PreviewIngredient_DoesNotMutateState();
        Test_SkipBowl_WithCandidates_Throws();
        Test_SelectIngredient_Or_SkipBowl_OutsideSelectionPhase_Throws();
        Test_FinalPot_SelectIngredient_AccumulatesUntilEndCooking();
        Test_EndCooking_NonFinalPot_Throws();

        Console.WriteLine("All GameControllerTests passed.");
    }

    // ── 辅助 ─────────────────────────────────────────────────────────────────

    static IngredientInstance FirstRiceCandidate(GameController gc)
    {
        var rice = gc.CurrentCandidates.FirstOrDefault(c => c.Definition.Id == "rice");
        Assert(rice != null, "候选中应存在米饭（初始池有 5 米饭 + 1 辣椒）");
        return rice!;
    }

    /// <summary>用尽可能多的选择 + 池空后的跳过走完当前锅。</summary>
    static void FinishCurrentPot(GameController gc)
    {
        int guard = 0;
        while (gc.Pot.Phase == PotPhase.InProgress && guard++ < 200)
        {
            if (gc.CanSelectIngredient)
                gc.SelectIngredient(gc.CurrentCandidates[0].InstanceId);
            else if (gc.CanSkipBowl)
                gc.SkipBowl();
            else
                break;
        }
        Assert(gc.Pot.Phase == PotPhase.Ended, "FinishCurrentPot: 当前锅应已 Ended");
    }

    /// <summary>推进 RunController 走完全部9锅普通锅（空锅），进入 Final Pot。</summary>
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
        Assert(state.Run.IsFinalPot, "AdvanceToFinalPot: 应已进入 Final Pot");
    }

    // ── 测试 ─────────────────────────────────────────────────────────────────

    /// <summary>StartNewGame 后应建立初始篮（5 米饭 + 1 辣椒）、本锅池并抽到 3 个候选。</summary>
    static void Test_StartNewGame_InitialState()
    {
        var gc = new GameController();
        gc.StartNewGame();

        Assert(gc.Run.Chapter == 1 && gc.Run.PotIndex == 1, "StartNewGame 后应处于第1章第1锅");
        Assert(gc.Pot.Phase == PotPhase.InProgress, "StartNewGame 后锅应为 InProgress");
        Assert(gc.Pot.CurrentBowlPhase == BowlPhase.IngredientSelection,
            "StartNewGame 后应处于 IngredientSelection");

        Assert(gc.Player.IngredientBasket.Count == 6, "初始食材篮应为 6");
        Assert(gc.Player.IngredientBasket.Count(i => i.Definition.Id == "rice") == 5, "初始篮应有 5 个米饭");
        Assert(gc.Player.IngredientBasket.Count(i => i.Definition.Id == "pepper") == 1, "初始篮应有 1 个辣椒");

        Assert(gc.CurrentCandidates.Count == 3, "应抽到 3 个候选");
        Assert(gc.CanSelectIngredient, "StartNewGame 后 CanSelectIngredient 应为 true");
        Assert(!gc.CanSkipBowl, "有候选时 CanSkipBowl 应为 false");
        Assert(gc.RemainingPoolCount == 6, "本锅池总数应为 6");
    }

    /// <summary>选中一个米饭：进入锅、基础分/味道生效、本锅池减少 1，但食材篮不变。</summary>
    static void Test_SelectIngredient_AddsToPot_ConsumesPool_KeepsBasket()
    {
        var gc = new GameController();
        gc.StartNewGame();

        var rice = FirstRiceCandidate(gc);
        int basketBefore = gc.Player.IngredientBasket.Count;

        gc.SelectIngredient(rice.InstanceId);

        Assert(gc.Pot.Ingredients.Any(i => i.InstanceId == rice.InstanceId),
            "选中实例应进入 Pot.Ingredients");
        Assert(gc.Pot.TotalBaseScore == 1, "米饭基础分 1 应计入 TotalBaseScore");
        Assert(gc.Pot.GetFlavor(FlavorType.Umami) == 1, "米饭应使鲜味 +1");
        Assert(gc.RemainingPoolCount == 5, "本锅池应从 6 减少到 5");
        Assert(gc.Player.IngredientBasket.Count == basketBefore,
            "选择食材不得消耗食材篮");
        Assert(gc.Player.IngredientBasket.Count == 6, "食材篮仍应为 6");
    }

    /// <summary>未选中的 2 个候选回池；剩余不足 3 个时 Draw 返回全部。</summary>
    static void Test_UnselectedCandidates_ReturnToPool_DrawAllWhenPoolBelowThree()
    {
        var gc = new GameController();
        gc.StartNewGame();

        Assert(gc.RemainingPoolCount == 6, "初始本锅池总数应为 6");
        gc.SelectIngredient(gc.CurrentCandidates[0].InstanceId);
        Assert(gc.RemainingPoolCount == 5, "选择 1 个后：未选 2 个回池，本锅池应为 5");

        // 继续选择直到本锅池只剩 2 个，此时 Draw 应把剩余的 2 个全部抽出
        while (gc.RemainingPoolCount > 2)
        {
            Assert(gc.CanSelectIngredient, "本锅池未空时应仍有候选");
            gc.SelectIngredient(gc.CurrentCandidates[0].InstanceId);
        }

        Assert(gc.RemainingPoolCount == 2, "本锅池应剩 2 个");
        Assert(gc.CurrentCandidates.Count == 2, "剩余不足 3 个时 Draw 应返回全部 2 个");
        Assert(gc.CurrentCandidates.Select(c => c.InstanceId).Distinct().Count() == 2,
            "候选实例不得重复");
    }

    /// <summary>仅用初始 6 个食材：连续选择 6 次后池空，第 7 碗起只能跳过，10 碗后锅 Ended。</summary>
    static void Test_PoolExhausted_SixSelections_ThenSkipToEnd()
    {
        var gc = new GameController();
        gc.StartNewGame();

        for (int i = 1; i <= 6; i++)
        {
            Assert(gc.CanSelectIngredient, $"第 {i} 次选择前应有候选");
            gc.SelectIngredient(gc.CurrentCandidates[0].InstanceId);
        }

        Assert(gc.RemainingPoolCount == 0, "6 次选择后本锅池应为空");
        Assert(gc.Pot.Phase == PotPhase.InProgress, "池空后锅仍应进行中");
        Assert(gc.Pot.BowlNumber == 7, "6 次选择后应进入第 7 碗");
        Assert(gc.CurrentCandidates.Count == 0, "池空时候选应为空");
        Assert(!gc.CanSelectIngredient, "池空时 CanSelectIngredient 应为 false");
        Assert(gc.CanSkipBowl, "池空时 CanSkipBowl 应为 true");

        for (int bowl = 7; bowl <= 10; bowl++)
        {
            Assert(gc.CanSkipBowl, $"第 {bowl} 碗应可跳过");
            gc.SkipBowl();
        }

        Assert(gc.Pot.BowlNumber == 10, "跳过后碗数应为 10");
        Assert(gc.Pot.Phase == PotPhase.Ended, "10 碗后锅应 Ended");
        Assert(gc.RemainingPoolCount == 0, "锅结束后本锅池应作废");
    }

    /// <summary>跨锅重建：第 1 锅结束后向篮加入蜂蜜，第 2 锅池应基于当时的篮重建，且篮不被消耗。</summary>
    static void Test_AdvanceToNextPot_RebuildsPoolFromBasket()
    {
        var gc = new GameController();
        gc.StartNewGame();

        FinishCurrentPot(gc);
        Assert(gc.RemainingPoolCount == 0, "第 1 锅结束后本锅池应作废");

        // 第 1 锅结束之后、第 2 锅开始之前：向篮加入 1 个蜂蜜
        gc.Player.IngredientBasket.Add(IngredientData.CreateInstance("honey"));
        int basketBeforePot2 = gc.Player.IngredientBasket.Count;

        gc.AdvanceToNextPot();

        Assert(gc.Run.PotIndex == 2, "应进入第 2 锅");
        Assert(gc.Player.IngredientBasket.Count == basketBeforePot2, "跨锅后食材篮不应被消耗");
        Assert(gc.RemainingPoolCount == basketBeforePot2,
            "新锅池应从当前篮重建，数量等于当时篮的数量");
        Assert(gc.CurrentCandidates.Count == 3, "新锅应从重建的池中抽到 3 个候选");

        var basketIds = gc.Player.IngredientBasket.Select(i => i.InstanceId).ToHashSet();
        Assert(gc.CurrentCandidates.All(c => basketIds.Contains(c.InstanceId)),
            "新锅候选应来自当前食材篮");
        Assert(gc.Pot.Ingredients.Count == 0, "新锅不应残留上一锅食材");
    }

    /// <summary>PreviewIngredient 使用候选实例预测且不修改真实状态。</summary>
    static void Test_PreviewIngredient_DoesNotMutateState()
    {
        var gc = new GameController();
        gc.StartNewGame();

        var rice = FirstRiceCandidate(gc);
        int baseBefore = gc.Pot.BaseScore;
        int umamiBefore = gc.Pot.GetFlavor(FlavorType.Umami);
        int totalBefore = gc.Pot.TotalBaseScore;
        int poolBefore = gc.RemainingPoolCount;
        int candidatesBefore = gc.CurrentCandidates.Count;
        int ingredientsBefore = gc.Pot.Ingredients.Count;
        var phaseBefore = gc.Pot.CurrentBowlPhase;

        var preview = gc.PreviewIngredient(rice);

        Assert(preview.PreviewBaseScore == baseBefore + rice.Definition.BaseScore,
            "米饭预览应为基础分 +1");
        Assert(gc.Pot.BaseScore == baseBefore, "预览不得修改真实 BaseScore");
        Assert(gc.Pot.GetFlavor(FlavorType.Umami) == umamiBefore, "预览不得修改真实味道");
        Assert(gc.Pot.TotalBaseScore == totalBefore, "预览不得修改 TotalBaseScore");
        Assert(gc.RemainingPoolCount == poolBefore, "预览不得修改本锅池");
        Assert(gc.CurrentCandidates.Count == candidatesBefore, "预览不得修改候选");
        Assert(gc.Pot.Ingredients.Count == ingredientsBefore, "预览不得向锅中添加食材");
        Assert(gc.Pot.CurrentBowlPhase == phaseBefore, "预览不得修改碗阶段");
    }

    /// <summary>有候选时（CanSkipBowl == false）调用 SkipBowl 应抛异常。</summary>
    static void Test_SkipBowl_WithCandidates_Throws()
    {
        var gc = new GameController();
        gc.StartNewGame();

        Assert(!gc.CanSkipBowl, "有候选时 CanSkipBowl 应为 false");

        bool threw = false;
        try { gc.SkipBowl(); }
        catch (InvalidOperationException) { threw = true; }

        Assert(threw, "有候选时 SkipBowl 应抛 InvalidOperationException");
    }

    /// <summary>非选择阶段调用 SelectIngredient / SkipBowl 应抛异常（安全失败）。</summary>
    static void Test_SelectIngredient_Or_SkipBowl_OutsideSelectionPhase_Throws()
    {
        var state = new GameState();
        var run = new RunController(state);
        run.StartRun();
        run.StartCurrentPot(); // 停在 BowlPhase.Start

        var gc = new GameController(state);

        Assert(!gc.CanSelectIngredient, "非 Selection 阶段 CanSelectIngredient 应为 false");
        Assert(!gc.CanSkipBowl, "非 Selection 阶段 CanSkipBowl 应为 false");

        bool selectThrew = false;
        try { gc.SelectIngredient("none"); }
        catch (InvalidOperationException) { selectThrew = true; }
        Assert(selectThrew, "非选择阶段 SelectIngredient 应抛 InvalidOperationException");

        bool skipThrew = false;
        try { gc.SkipBowl(); }
        catch (InvalidOperationException) { skipThrew = true; }
        Assert(skipThrew, "非选择阶段 SkipBowl 应抛 InvalidOperationException");
    }

    /// <summary>最终锅：可反复选择累积基础分且锅不 Ended；EndCooking 后整锅结算并完成 Run。</summary>
    static void Test_FinalPot_SelectIngredient_AccumulatesUntilEndCooking()
    {
        var state = new GameState();
        var run = new RunController(state);
        run.StartRun();
        AdvanceToFinalPot(run, state);

        state.Player.IngredientBasket.AddRange(IngredientData.CreateInitialBasket());
        for (int i = 0; i < 4; i++)
            state.Player.IngredientBasket.Add(IngredientData.CreateInstance("rice"));

        var gc = new GameController(state);
        gc.StartCurrentPot();

        Assert(gc.IsFinalPot, "构造后应处于最终锅");
        Assert(gc.Pot.Phase == PotPhase.InProgress, "最终锅应为 InProgress");
        Assert(gc.Pot.BowlNumber == ScoreCalculator.FinalPotBowlNumber,
            "最终锅碗数应固定为 ×32 档位");
        Assert(gc.CurrentCandidates.Count == 3, "最终锅也应抽到 3 个候选");

        int selections = 0;
        int lastBase = gc.Pot.BaseScore;
        while (gc.CanSelectIngredient && selections < 20)
        {
            gc.SelectIngredient(gc.CurrentCandidates[0].InstanceId);
            selections++;

            Assert(gc.Pot.Phase == PotPhase.InProgress, "最终锅选择食材不应结束锅");
            Assert(gc.Pot.BowlNumber == ScoreCalculator.FinalPotBowlNumber,
                "最终锅选择食材不应推进碗数");
            Assert(gc.Pot.BaseScore >= lastBase, "最终锅基础分应持续累积");
            lastBase = gc.Pot.BaseScore;
        }

        Assert(selections >= 6, "最终锅应能持续选择多次");
        Assert(gc.Pot.BaseScore > 0, "最终锅累积基础分应大于 0");
        Assert(!gc.CanSkipBowl, "最终锅不允许跳过");

        gc.EndCooking();

        Assert(gc.Pot.Phase == PotPhase.Ended, "EndCooking 后锅应 Ended");
        Assert(run.IsRunComplete, "EndCooking 后本局应完成");
    }

    /// <summary>非最终锅调用 EndCooking 应抛异常。</summary>
    static void Test_EndCooking_NonFinalPot_Throws()
    {
        var gc = new GameController();
        gc.StartNewGame();

        bool threw = false;
        try { gc.EndCooking(); }
        catch (InvalidOperationException) { threw = true; }

        Assert(threw, "非最终锅 EndCooking 应抛 InvalidOperationException");
    }

    static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception($"[FAIL] GameControllerTests: {message}");
    }
}
