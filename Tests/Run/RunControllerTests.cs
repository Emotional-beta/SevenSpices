using SevenSpices.Core.Game;
using SevenSpices.Core.Pot;
using SevenSpices.Core.Run;
using SevenSpices.Core.Scoring;

namespace SevenSpices.Tests.Run;

/// <summary>
/// RunController 测试：验证 Run 层对 Chapter / Pot 推进的驱动能力。
/// Phase 4 Part 2：最小 Run / Chapter / Pot 推进流程。
/// </summary>
public static class RunControllerTests
{
    public static void RunAll()
    {
        // StartRun
        Test_StartRun_InitializesCorrectly();
        Test_StartRun_ResetsRunState();

        // StartCurrentPot
        Test_StartCurrentPot_PotIsInProgress();
        Test_StartCurrentPot_PotStateIsReset();
        Test_StartCurrentPot_AppliesBottomToPot();

        // 普通锅推进
        Test_AdvanceToNextPot_IncrementsPotIndex();
        Test_AdvanceToNextPot_RequiresPotEnded();
        Test_AdvanceToNextPot_WrapsToNextChapter();

        // 多锅连续推进
        Test_MultiPot_AllNinePotsAdvanceCorrectly();
        Test_MultiPot_StateConsistency();

        // 锅底跨锅滚雪球（设计文档 §14 / §12.2）
        Test_Bottom_SnowballsAcrossPots();

        // Final Pot
        Test_AfterAllNormalPots_IsFinalPotIsTrue();
        Test_FinalPot_BowlLimitIsMaxValue();
        Test_FinalPot_CannotAdvanceFurther();
        Test_FinalPot_EndPotAndClosePot_RunIsComplete();

        Console.WriteLine("All RunControllerTests passed.");
    }

    // ── 辅助 ─────────────────────────────────────────────────────────────────

    /// <summary>快速跑完一整锅（10碗），返回已 Ended 的 PotController。不调用 ClosePot。</summary>
    static PotController RunPotToEnd(PotController ctrl)
    {
        for (int bowl = 1; bowl <= 10; bowl++)
        {
            ctrl.StartBowl();
            // 推进全部 9 步（Start → End）
            for (int step = 0; step < 9; step++)
                ctrl.AdvanceBowlPhase();

            if (bowl < 10)
                ctrl.StartNextBowl();
            // 第10碗到 End 时锅自动 Ended
        }
        return ctrl;
    }

    // ── StartRun ─────────────────────────────────────────────────────────────

    static void Test_StartRun_InitializesCorrectly()
    {
        var state = new GameState();
        var run = new RunController(state);
        run.StartRun();

        Assert(state.Run.Chapter == 1, "StartRun 后 Chapter 应为 1");
        Assert(state.Run.PotIndex == 1, "StartRun 后 PotIndex 应为 1");
        Assert(!state.Run.IsFinalPot, "StartRun 后 IsFinalPot 应为 false");
    }

    static void Test_StartRun_ResetsRunState()
    {
        var state = new GameState();
        // 人为设置到 Chapter 2, PotIndex 3
        state.Run.Chapter = 2;
        state.Run.PotIndex = 3;
        state.Run.IsFinalPot = true;

        var run = new RunController(state);
        run.StartRun();

        Assert(state.Run.Chapter == 1, "StartRun 应将 Chapter 重置为 1");
        Assert(state.Run.PotIndex == 1, "StartRun 应将 PotIndex 重置为 1");
        Assert(!state.Run.IsFinalPot, "StartRun 应将 IsFinalPot 重置为 false");
    }

    // ── StartCurrentPot ───────────────────────────────────────────────────────

    static void Test_StartCurrentPot_PotIsInProgress()
    {
        var state = new GameState();
        var run = new RunController(state);
        run.StartRun();

        var ctrl = run.StartCurrentPot();

        Assert(state.Pot.Phase == PotPhase.InProgress, "StartCurrentPot 后锅应为 InProgress");
        Assert(ctrl != null, "StartCurrentPot 应返回 PotController 实例");
    }

    static void Test_StartCurrentPot_PotStateIsReset()
    {
        var state = new GameState();
        var run = new RunController(state);
        run.StartRun();

        // 先走完第一锅
        var ctrl1 = run.StartCurrentPot();
        RunPotToEnd(ctrl1);
        ctrl1.ClosePot();
        run.AdvanceToNextPot();

        // 人为在 PotState 留下"上一锅的痕迹"
        // （实际上 ClosePot + AdvanceToNextPot 后锅已经 Ended，PotState 保留了食材等）
        Assert(state.Pot.Phase == PotPhase.Ended, "第一锅结束后应为 Ended");

        // 启动第二锅
        var ctrl2 = run.StartCurrentPot();

        Assert(state.Pot.Phase == PotPhase.InProgress, "第二锅启动后应为 InProgress");
        Assert(state.Pot.BowlNumber == 1, "第二锅 BowlNumber 应重置为 1");
        Assert(state.Pot.BowlLimit == 10, "第二锅 BowlLimit 应为 10");
        Assert(state.Pot.BaseScore == 0, "第二锅 BaseScore 应重置为 0");
        Assert(!state.Pot.IsScoreLocked, "第二锅 IsScoreLocked 应重置为 false");
        Assert(state.Pot.Ingredients.Count == 0, "第二锅食材列表应为空");
    }

    static void Test_StartCurrentPot_AppliesBottomToPot()
    {
        var state = new GameState();
        // 预先设置锅底
        state.Bottom.SetFlavor(FlavorType.Sweet, 5);

        var run = new RunController(state);
        run.StartRun();
        run.StartCurrentPot();

        Assert(state.Pot.GetFlavor(FlavorType.Sweet) == 5,
            "StartCurrentPot 应将锅底注入 PotState");
    }

    // ── 普通锅推进 ─────────────────────────────────────────────────────────────

    static void Test_AdvanceToNextPot_IncrementsPotIndex()
    {
        var state = new GameState();
        var run = new RunController(state);
        run.StartRun();

        var ctrl = run.StartCurrentPot();
        RunPotToEnd(ctrl);
        ctrl.ClosePot();

        run.AdvanceToNextPot();

        Assert(state.Run.Chapter == 1, "同章节内推进后 Chapter 应不变");
        Assert(state.Run.PotIndex == 2, "第一锅完成后 PotIndex 应为 2");
        Assert(!state.Run.IsFinalPot, "仍有普通锅时 IsFinalPot 应为 false");
    }

    static void Test_AdvanceToNextPot_RequiresPotEnded()
    {
        var state = new GameState();
        var run = new RunController(state);
        run.StartRun();
        run.StartCurrentPot(); // InProgress

        bool threw = false;
        try { run.AdvanceToNextPot(); }
        catch (InvalidOperationException) { threw = true; }

        Assert(threw, "锅仍在进行中时 AdvanceToNextPot 应抛 InvalidOperationException");
        Assert(state.Run.PotIndex == 1, "抛异常后 PotIndex 不应变化");
    }

    static void Test_AdvanceToNextPot_WrapsToNextChapter()
    {
        var state = new GameState();
        var run = new RunController(state);
        run.StartRun();

        // 走完 Chapter 1 的全部3锅
        for (int i = 0; i < RunController.PotsPerChapter; i++)
        {
            var ctrl = run.StartCurrentPot();
            RunPotToEnd(ctrl);
            ctrl.ClosePot();

            if (i < RunController.PotsPerChapter - 1)
                run.AdvanceToNextPot();
            else
            {
                // 第3锅完成后 → 进入 Chapter 2
                run.AdvanceToNextPot();
                Assert(state.Run.Chapter == 2, "Chapter 1 全部锅完成后应进入 Chapter 2");
                Assert(state.Run.PotIndex == 1, "新 Chapter 的 PotIndex 应重置为 1");
            }
        }
    }

    // ── 多锅连续推进 ───────────────────────────────────────────────────────────

    static void Test_MultiPot_AllNinePotsAdvanceCorrectly()
    {
        var state = new GameState();
        var run = new RunController(state);
        run.StartRun();

        // 预期序列：(Chapter, PotIndex)
        var expected = new (int ch, int pi)[]
        {
            (1,1),(1,2),(1,3),
            (2,1),(2,2),(2,3),
            (3,1),(3,2),(3,3),
        };

        for (int i = 0; i < expected.Length; i++)
        {
            var (ch, pi) = expected[i];
            Assert(state.Run.Chapter == ch,
                $"锅{i+1}开始时 Chapter 应为 {ch}，实际为 {state.Run.Chapter}");
            Assert(state.Run.PotIndex == pi,
                $"锅{i+1}开始时 PotIndex 应为 {pi}，实际为 {state.Run.PotIndex}");

            var ctrl = run.StartCurrentPot();
            Assert(state.Pot.Phase == PotPhase.InProgress,
                $"锅{i+1}启动后应为 InProgress");
            RunPotToEnd(ctrl);
            ctrl.ClosePot();

            if (i < expected.Length - 1)
            {
                run.AdvanceToNextPot();
                Assert(!state.Run.IsFinalPot,
                    $"第{i+1}锅完成后不应提前进入 IsFinalPot");
            }
        }

        // 最后一锅（Chapter 3 Pot 3）结束后推进 → Final Pot
        run.AdvanceToNextPot();
        Assert(state.Run.IsFinalPot, "9锅全部完成后应进入 Final Pot");
    }

    static void Test_MultiPot_StateConsistency()
    {
        // 验证：每次 StartCurrentPot 后，RunState 反映的锅与 PotState 实际运行的锅一致。
        var state = new GameState();
        var run = new RunController(state);
        run.StartRun();

        // Chapter 1 Pot 1：启动，确认状态一致
        var ctrl1 = run.StartCurrentPot();
        Assert(state.Run.Chapter == 1 && state.Run.PotIndex == 1,
            "Chapter 1 Pot 1：RunState 应为 (1,1)");
        Assert(state.Pot.Phase == PotPhase.InProgress,
            "Chapter 1 Pot 1：PotState 应为 InProgress");
        RunPotToEnd(ctrl1);
        ctrl1.ClosePot();
        run.AdvanceToNextPot();

        // Chapter 1 Pot 2：启动，确认状态一致
        var ctrl2 = run.StartCurrentPot();
        Assert(state.Run.Chapter == 1 && state.Run.PotIndex == 2,
            "Chapter 1 Pot 2：RunState 应为 (1,2)");
        Assert(state.Pot.Phase == PotPhase.InProgress,
            "Chapter 1 Pot 2：PotState 应为 InProgress（新锅已重置）");

        // 第一锅的 PotController 已失效，不能再操作（锅已 Ended 并 Reset）
        // 验证 ctrl1 对应的 Pot 已经被覆盖（通过检查 state.Pot 是同一个被重置的实例）
        Assert(state.Pot.BowlNumber == 1,
            "第二锅启动后 BowlNumber 应为 1（已 Reset）");
    }

    // ── 锅底跨锅滚雪球 ────────────────────────────────────────────────────────

    /// <summary>
    /// 设计文档 §14 滚雪球：第1锅 20甜 → 锅底 6；第2锅 6+20=26 → 提炼 7。
    /// 同时验证 PotState.Reset + BottomState.ApplyToPot 不会丢掉或重复衰减锅底。
    /// </summary>
    static void Test_Bottom_SnowballsAcrossPots()
    {
        var state = new GameState();
        var run = new RunController(state);
        run.StartRun();

        // 第 1 锅
        var ctrl1 = run.StartCurrentPot();
        state.Pot.AddFlavor(FlavorType.Sweet, 20);
        RunPotToEnd(ctrl1);
        ctrl1.ClosePot();
        Assert(state.Bottom.GetFlavor(FlavorType.Sweet) == 6, "第1锅 20甜 → 锅底 6");

        run.AdvanceToNextPot();

        // 第 2 锅：开锅即注入旧锅底 6
        var ctrl2 = run.StartCurrentPot();
        Assert(state.Pot.GetFlavor(FlavorType.Sweet) == 6,
            "第2锅开锅应直接注入锅底 6 甜（不再次乘 30%）");

        state.Pot.AddFlavor(FlavorType.Sweet, 20);
        Assert(state.Pot.GetFlavor(FlavorType.Sweet) == 26, "第2锅最终应为 26 甜");

        RunPotToEnd(ctrl2);
        ctrl2.ClosePot();
        Assert(state.Bottom.GetFlavor(FlavorType.Sweet) == 7, "26 × 30% = 7.8 → 锅底 7");
    }

    // ── Final Pot ─────────────────────────────────────────────────────────────

    static void Test_AfterAllNormalPots_IsFinalPotIsTrue()
    {
        var state = new GameState();
        var run = new RunController(state);
        run.StartRun();

        // 连续推进 9 锅
        for (int i = 0; i < RunController.ChaptersPerRun * RunController.PotsPerChapter; i++)
        {
            var ctrl = run.StartCurrentPot();
            RunPotToEnd(ctrl);
            ctrl.ClosePot();
            run.AdvanceToNextPot();
        }

        Assert(state.Run.IsFinalPot,
            "所有普通锅完成后 IsFinalPot 应为 true");
    }

    static void Test_FinalPot_BowlLimitIsMaxValue()
    {
        var state = new GameState();
        var run = new RunController(state);
        run.StartRun();

        AdvanceRunToFinalPot(run, state);

        var ctrl = run.StartCurrentPot();
        Assert(state.Pot.BowlLimit == int.MaxValue,
            "最终锅 BowlLimit 应为 int.MaxValue");
        Assert(state.Pot.Phase == PotPhase.InProgress,
            "最终锅应成功启动");
    }

    static void Test_FinalPot_CannotAdvanceFurther()
    {
        var state = new GameState();
        var run = new RunController(state);
        run.StartRun();
        AdvanceRunToFinalPot(run, state);

        // 强制将 Pot 状态设为 Ended 以满足 AdvanceToNextPot 的前置条件
        // 但此时 IsFinalPot = true，应该抛异常
        state.Run.IsFinalPot = true;
        // 直接设置 Phase 模拟已结束
        state.Pot.Reset(int.MaxValue);
        state.Pot.Phase = PotPhase.Ended;

        bool threw = false;
        try { run.AdvanceToNextPot(); }
        catch (InvalidOperationException) { threw = true; }

        Assert(threw, "IsFinalPot 为 true 时调用 AdvanceToNextPot 应抛 InvalidOperationException");
    }

    static void Test_FinalPot_EndPotAndClosePot_RunIsComplete()
    {
        var state = new GameState();
        var run = new RunController(state);
        run.StartRun();
        AdvanceRunToFinalPot(run, state);

        var ctrl = run.StartCurrentPot();
        Assert(!run.IsRunComplete, "最终锅进行中时 IsRunComplete 应为 false");
        Assert(state.Pot.BowlNumber == ScoreCalculator.FinalPotBowlNumber,
            "最终锅 BowlNumber 应固定为 ×32 档位");

        // 最终锅不逐碗结算：走完一碗后主动结束最终锅
        ctrl.StartBowl();
        for (int s = 0; s < 9; s++) ctrl.AdvanceBowlPhase();
        Assert(state.Pot.Phase == PotPhase.InProgress,
            "最终锅走到碗末后仍应保持 InProgress");
        ctrl.EndPot();
        ctrl.ClosePot();

        Assert(run.IsRunComplete, "最终锅结束后 IsRunComplete 应为 true");
        Assert(state.Run.IsFinalPot, "结束后 IsFinalPot 应保持 true");
        Assert(state.Pot.Phase == PotPhase.Ended, "结束后 PotPhase 应为 Ended");
    }

    // ── 工具 ──────────────────────────────────────────────────────────────────

    /// <summary>推进 Run 经过全部9锅普通锅，最终让 IsFinalPot = true。</summary>
    static void AdvanceRunToFinalPot(RunController run, GameState state)
    {
        int totalNormalPots = RunController.ChaptersPerRun * RunController.PotsPerChapter;
        for (int i = 0; i < totalNormalPots; i++)
        {
            var ctrl = run.StartCurrentPot();
            RunPotToEnd(ctrl);
            ctrl.ClosePot();
            run.AdvanceToNextPot();
        }
        Assert(state.Run.IsFinalPot, "AdvanceRunToFinalPot: IsFinalPot 应已为 true");
    }

    static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception($"[FAIL] RunControllerTests: {message}");
    }
}
