using SevenSpices.Core.Game;
using SevenSpices.Core.Pot;
using SevenSpices.Core.Run;

namespace SevenSpices.Tests.Game;

/// <summary>
/// GameController（Game Layer 编排）测试：验证对表现层暴露的统一入口行为与现有流程等价。
/// A1：行为保持重构。
/// </summary>
public static class GameControllerTests
{
    public static void RunAll()
    {
        Test_StartNewGame_InitialState();
        Test_AddIngredient_Rice_RaisesBaseScoreAndUmami();
        Test_NormalPot_TenAdditions_AdvancesBowlsAndEndsPot();
        Test_PreviewIngredient_DoesNotMutateState();
        Test_CanAddIngredient_False_BeforeBowlAdvanced();
        Test_EndCooking_NonFinalPot_Throws();
        Test_EndCooking_FinalPot_Completes();

        Console.WriteLine("All GameControllerTests passed.");
    }

    // ── 辅助 ─────────────────────────────────────────────────────────────────

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

    /// <summary>
    /// 构造一个处于最终锅 IngredientResolve 阶段的 GameController（注入状态）。
    /// 最终锅不逐碗推进，便于直接观察 BaseScore / Flavor 的变化。
    /// </summary>
    static GameController MakeFinalPotController()
    {
        var state = new GameState();
        var run = new RunController(state);
        run.StartRun();
        AdvanceToFinalPot(run, state);

        var ctrl = run.StartCurrentPot();
        ctrl.StartBowl();
        for (int i = 0; i < 4; i++) ctrl.AdvanceBowlPhase();

        return new GameController(state);
    }

    // ── 测试 ─────────────────────────────────────────────────────────────────

    /// <summary>StartNewGame 后应完成 Run/锅/碗的启动并推进到 IngredientResolve。</summary>
    static void Test_StartNewGame_InitialState()
    {
        var gc = new GameController();
        gc.StartNewGame();

        Assert(gc.Run.Chapter == 1, "StartNewGame 后 Chapter=1");
        Assert(gc.Run.PotIndex == 1, "StartNewGame 后 PotIndex=1");
        Assert(gc.Pot.Phase == PotPhase.InProgress, "StartNewGame 后锅应为 InProgress");
        Assert(gc.Pot.CurrentBowlPhase == BowlPhase.IngredientResolve,
            "StartNewGame 后应推进到 IngredientResolve");
        Assert(gc.CanAddIngredient, "StartNewGame 后 CanAddIngredient 应为 true");
    }

    /// <summary>
    /// AddIngredient("rice") 应结算米饭基础分与鲜味。
    /// 普通锅会自动整碗推进并进入下一碗，BackScore 随 StartBowl 归零，
    /// 因此用最终锅（不推进）直接观察 BaseScore，同时在普通锅用 TotalBaseScore 验证。
    /// </summary>
    static void Test_AddIngredient_Rice_RaisesBaseScoreAndUmami()
    {
        // 普通锅：BaseScore 累加到 TotalBaseScore（不随下一碗重置）
        var normal = new GameController();
        normal.StartNewGame();
        normal.AddIngredient("rice");
        Assert(normal.Pot.TotalBaseScore == 1, "普通锅投入米饭后 TotalBaseScore 应 +1");
        Assert(normal.Pot.GetFlavor(FlavorType.Umami) == 1, "普通锅投入米饭后鲜味 +1");

        // 最终锅：不逐碗推进，可直接观察 BaseScore
        var final = MakeFinalPotController();
        final.AddIngredient("rice");
        Assert(final.Pot.BaseScore == 1, "最终锅投入米饭后 BaseScore 应 +1");
        Assert(final.Pot.GetFlavor(FlavorType.Umami) == 1, "最终锅投入米饭后鲜味 +1");
        Assert(final.Pot.CurrentBowlPhase == BowlPhase.IngredientResolve,
            "最终锅投入食材后不应推进碗阶段");
    }

    /// <summary>普通锅连续投入10次：每次进入下一碗的 IngredientResolve，10次后锅 Ended。</summary>
    static void Test_NormalPot_TenAdditions_AdvancesBowlsAndEndsPot()
    {
        var gc = new GameController();
        gc.StartNewGame();

        for (int i = 1; i <= 10; i++)
        {
            gc.AddIngredient("rice");

            if (i < 10)
            {
                Assert(gc.Pot.BowlNumber == i + 1,
                    $"第{i}次投入后应进入第{i + 1}碗");
                Assert(gc.Pot.CurrentBowlPhase == BowlPhase.IngredientResolve,
                    $"第{i}次投入后应处于 IngredientResolve");
                Assert(gc.Pot.Phase == PotPhase.InProgress,
                    $"第{i}次投入后锅仍应 InProgress");
                Assert(gc.CanAddIngredient, $"第{i}次投入后 CanAddIngredient 应为 true");
            }
        }

        Assert(gc.Pot.BowlNumber == 10, "10次投入后碗数应为 10");
        Assert(gc.Pot.Phase == PotPhase.Ended, "10次投入后锅应 Ended");
        Assert(!gc.CanAddIngredient, "锅 Ended 后 CanAddIngredient 应为 false");
    }

    /// <summary>PreviewIngredient 不修改真实状态。</summary>
    static void Test_PreviewIngredient_DoesNotMutateState()
    {
        var gc = new GameController();
        gc.StartNewGame();

        int baseBefore = gc.Pot.BaseScore;
        int umamiBefore = gc.Pot.GetFlavor(FlavorType.Umami);

        var preview = gc.PreviewIngredient("rice");

        Assert(preview.PreviewBaseScore == baseBefore + 1, "预览应给出投入后的 BaseScore");
        Assert(gc.Pot.BaseScore == baseBefore, "预览不应修改真实 BaseScore");
        Assert(gc.Pot.GetFlavor(FlavorType.Umami) == umamiBefore, "预览不应修改真实味道");
    }

    /// <summary>锅已开始但碗阶段尚未推进到 IngredientResolve 时不可投入食材。</summary>
    static void Test_CanAddIngredient_False_BeforeBowlAdvanced()
    {
        var state = new GameState();
        var run = new RunController(state);
        run.StartRun();
        run.StartCurrentPot();

        var gc = new GameController(state);

        Assert(gc.Pot.Phase == PotPhase.InProgress, "新开的锅应为 InProgress");
        Assert(gc.Pot.CurrentBowlPhase != BowlPhase.IngredientResolve,
            "新开的锅此时不应处于 IngredientResolve");
        Assert(!gc.CanAddIngredient, "非 IngredientResolve 阶段 CanAddIngredient 应为 false");
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

    /// <summary>最终锅 EndCooking 应完成结算并标记 Run 完成。</summary>
    static void Test_EndCooking_FinalPot_Completes()
    {
        var state = new GameState();
        var run = new RunController(state);
        run.StartRun();
        AdvanceToFinalPot(run, state);
        run.StartCurrentPot();

        var gc = new GameController(state);

        Assert(gc.IsFinalPot, "构造后应处于最终锅");
        Assert(!run.IsRunComplete, "EndCooking 前 IsRunComplete 应为 false");

        gc.EndCooking();

        Assert(state.Pot.Phase == PotPhase.Ended, "EndCooking 后锅应 Ended");
        Assert(run.IsRunComplete, "EndCooking 后 IsRunComplete 应为 true");
    }

    static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception($"[FAIL] GameControllerTests: {message}");
    }
}
