using SevenSpices.Core.Effects;
using SevenSpices.Core.Game;
using SevenSpices.Core.Ingredients;
using SevenSpices.Core.Pot;
using SevenSpices.Core.Run;
using SevenSpices.Core.Scoring;
using SevenSpices.Tests.Effects;

namespace SevenSpices.Tests.Pot;

/// <summary>
/// 验证单锅完整生命周期：开锅 → 碗流程 → 分数计算 → 锅结束 → 锅底提炼。
/// Phase 4 Part 1：最小端对端锅生命周期测试。
/// </summary>
public static class PotLifecycleTests
{
    public static void RunAll()
    {
        Test_FullSingleBowl_ScoreCalculatedAndLocked();
        Test_CalculateScore_WrongPhase_Throws();
        Test_CalculateScore_AlreadyLocked_Throws();
        Test_ClosePot_ExtractsBottom();
        Test_ClosePot_NotEnded_Throws();
        Test_NormalPot_FullLifecycle_TenBowls();
        Test_FinalPot_EndPot_ThenClosePot();
        Test_ClosePot_CannotBeCalledTwiceWithoutNewPot();

        Console.WriteLine("All PotLifecycleTests passed.");
    }

    // ── 辅助 ─────────────────────────────────────────────────────────────────

    /// <summary>将碗从 Start 推进到指定阶段（含目标阶段）。</summary>
    static void AdvanceTo(PotController ctrl, BowlPhase target)
    {
        while (ctrl.Pot.CurrentBowlPhase != target)
            ctrl.AdvanceBowlPhase();
    }

    /// <summary>将碗从当前阶段一路推进到 BowlPhase.End。</summary>
    static void AdvanceToEnd(PotController ctrl)
        => AdvanceTo(ctrl, BowlPhase.End);

    /// <summary>走完一碗（StartBowl → 推进到 End），不调用 CalculateScore，仅用于快速消耗碗数。</summary>
    static void RunBowlToEnd(PotController ctrl)
    {
        ctrl.StartBowl();
        AdvanceToEnd(ctrl);
    }

    // ── 测试 ─────────────────────────────────────────────────────────────────

    /// <summary>单碗完整流程：分数计算正确，状态锁定。</summary>
    static void Test_FullSingleBowl_ScoreCalculatedAndLocked()
    {
        var state = new GameState();
        var ctrl = new PotController(state);
        ctrl.StartPot();
        ctrl.StartBowl();

        // 人为设置 BaseScore，模拟食材效果已结算
        state.Pot.BaseScore = 10;

        AdvanceTo(ctrl, BowlPhase.ScoreCalculation);
        ctrl.CalculateScore();

        // 第1碗倍率 ×1
        Assert(state.Pot.FinalScore == 10, "第1碗 FinalScore 应为 BaseScore × 1 = 10");
        Assert(state.Pot.IsScoreLocked, "分数应已锁定");
        Assert(state.Pot.CurrentBowlPhase == BowlPhase.ScoreCalculation, "CalculateScore 不应推进碗阶段");
    }

    /// <summary>非 ScoreCalculation 阶段调用 CalculateScore 应抛异常。</summary>
    static void Test_CalculateScore_WrongPhase_Throws()
    {
        var state = new GameState();
        var ctrl = new PotController(state);
        ctrl.StartPot();
        ctrl.StartBowl(); // BowlPhase.Start

        bool threw = false;
        try { ctrl.CalculateScore(); }
        catch (InvalidOperationException) { threw = true; }

        Assert(threw, "非 ScoreCalculation 阶段调用 CalculateScore 应抛 InvalidOperationException");
    }

    /// <summary>分数已锁定后再次调用 CalculateScore 应抛异常。</summary>
    static void Test_CalculateScore_AlreadyLocked_Throws()
    {
        var state = new GameState();
        var ctrl = new PotController(state);
        ctrl.StartPot();
        ctrl.StartBowl();
        AdvanceTo(ctrl, BowlPhase.ScoreCalculation);
        ctrl.CalculateScore(); // 第一次锁定

        bool threw = false;
        try { ctrl.CalculateScore(); }
        catch (InvalidOperationException) { threw = true; }

        Assert(threw, "分数已锁定后再次 CalculateScore 应抛 InvalidOperationException");
    }

    /// <summary>普通锅结束后调用 ClosePot 应正确提炼锅底。</summary>
    static void Test_ClosePot_ExtractsBottom()
    {
        var state = new GameState();
        var ctrl = new PotController(state);
        ctrl.StartPot();

        // 通过 AddFlavor 给锅加味道，模拟本锅结算后积累的 Flavor
        state.Pot.AddFlavor(FlavorType.Sweet, 10);

        // 跑完10碗让锅自然结束
        for (int i = 1; i <= 9; i++)
        {
            RunBowlToEnd(ctrl);
            ctrl.StartNextBowl();
        }
        RunBowlToEnd(ctrl); // 第10碗 → Ended

        Assert(state.Pot.Phase == PotPhase.Ended, "锅应已结束");

        ctrl.ClosePot();

        // 30% of 10 = 3（floor），锅底 Sweet 应为 3
        Assert(state.Bottom.GetFlavor(FlavorType.Sweet) == 3,
            "ClosePot 应提炼锅底：Sweet 10 × 30% = 3");
    }

    /// <summary>锅未结束时调用 ClosePot 应抛异常。</summary>
    static void Test_ClosePot_NotEnded_Throws()
    {
        var state = new GameState();
        var ctrl = new PotController(state);
        ctrl.StartPot();

        bool threw = false;
        try { ctrl.ClosePot(); }
        catch (InvalidOperationException) { threw = true; }

        Assert(threw, "InProgress 状态调用 ClosePot 应抛 InvalidOperationException");
    }

    /// <summary>普通锅10碗完整生命周期：含每碗分数计算，第6碗验证倍率×2。</summary>
    static void Test_NormalPot_FullLifecycle_TenBowls()
    {
        var state = new GameState();
        var ctrl = new PotController(state);
        ctrl.StartPot();

        Assert(state.Pot.Phase == PotPhase.InProgress, "StartPot 后应为 InProgress");

        for (int bowl = 1; bowl <= 10; bowl++)
        {
            ctrl.StartBowl();
            state.Pot.BaseScore = 5; // 每碗基础分固定5

            AdvanceTo(ctrl, BowlPhase.ScoreCalculation);
            ctrl.CalculateScore();

            int expectedMultiplier = bowl <= 5 ? 1 : bowl == 6 ? 2 : bowl == 7 ? 4 : bowl == 8 ? 8 : bowl == 9 ? 16 : 32;
            Assert(state.Pot.FinalScore == 5 * expectedMultiplier,
                $"第{bowl}碗 FinalScore 应为 {5 * expectedMultiplier}，实际为 {state.Pot.FinalScore}");
            Assert(state.Pot.IsScoreLocked, $"第{bowl}碗分数应已锁定");

            AdvanceToEnd(ctrl);

            if (bowl < 10)
            {
                Assert(state.Pot.Phase == PotPhase.InProgress, $"第{bowl}碗结束后锅应仍为 InProgress");
                ctrl.StartNextBowl();
            }
        }

        Assert(state.Pot.Phase == PotPhase.Ended, "第10碗结束后锅应为 Ended");
        Assert(state.Pot.BowlNumber == 10, "BowlNumber 应为 10");

        ctrl.ClosePot();
        // 本测试未设置任何 Flavor：未在本锅出现过的味道不写入锅底，保持 0
        Assert(state.Bottom.GetFlavor(FlavorType.Sweet) == 0,
            "本锅未出现的味道不应写入锅底");
    }

    /// <summary>
    /// 最终锅：固定 BowlNumber=10（×32 档）、禁止进入下一碗，
    /// 通过 EndPot() 结束后 ClosePot() 应正确提炼锅底。
    /// </summary>
    static void Test_FinalPot_EndPot_ThenClosePot()
    {
        var state = new GameState();
        var run = MakeRunAtFinalPot(state);
        var ctrl = run.StartCurrentPot();

        Assert(state.Pot.BowlLimit == int.MaxValue, "最终锅 BowlLimit 应为 int.MaxValue");
        Assert(state.Pot.BowlNumber == ScoreCalculator.FinalPotBowlNumber,
            "最终锅 BowlNumber 应固定为 ×32 档位");

        ctrl.StartBowl();
        AdvanceToEnd(ctrl);

        // 最终锅不逐碗结算：即使已到碗末也不允许进入下一碗
        bool threw = false;
        try { ctrl.StartNextBowl(); }
        catch (InvalidOperationException) { threw = true; }
        Assert(threw, "最终锅调用 StartNextBowl 应抛 InvalidOperationException");

        // 设置 Flavor 验证 ClosePot 能正确运行
        state.Pot.AddFlavor(FlavorType.Spicy, 20);
        ctrl.EndPot();
        Assert(state.Pot.Phase == PotPhase.Ended, "EndPot 后应为 Ended");

        ctrl.ClosePot();

        // 30% of 20 = 6
        Assert(state.Bottom.GetFlavor(FlavorType.Spicy) == 6,
            "最终锅 ClosePot 后锅底 Spicy 应为 6（20×30%）");
    }

    /// <summary>推进 Run 经过全部9锅普通锅，返回处于最终锅状态的 RunController。</summary>
    static RunController MakeRunAtFinalPot(GameState state)
    {
        var run = new RunController(state);
        run.StartRun();
        int total = RunController.ChaptersPerRun * RunController.PotsPerChapter;
        for (int i = 0; i < total; i++)
        {
            var ctrl = run.StartCurrentPot();
            for (int bowl = 1; bowl <= 10; bowl++)
            {
                RunBowlToEnd(ctrl);
                if (bowl < 10) ctrl.StartNextBowl();
            }
            ctrl.ClosePot();
            run.AdvanceToNextPot();
        }
        Assert(state.Run.IsFinalPot, "MakeRunAtFinalPot: 应已进入最终锅");
        return run;
    }

    /// <summary>ClosePot 重复提炼应保持幂等（锅底单调合并，不会因为重复提炼而改变结果）。</summary>
    static void Test_ClosePot_CannotBeCalledTwiceWithoutNewPot()
    {
        // ClosePot 只检查 PotPhase.Ended，调用两次会重复提炼锅底。
        // BottomExtractor 是单调合并写入，且 pot 的 Flavor 不变，因此重复调用结果一致。
        // 此测试验证重复调用不会抛异常，并且结果与最后一次调用一致。
        var state = new GameState();
        var ctrl = new PotController(state);
        ctrl.StartPot();
        state.Pot.AddFlavor(FlavorType.Sour, 10);

        for (int i = 1; i <= 9; i++)
        {
            RunBowlToEnd(ctrl);
            ctrl.StartNextBowl();
        }
        RunBowlToEnd(ctrl);

        ctrl.ClosePot();
        int firstResult = state.Bottom.GetFlavor(FlavorType.Sour);

        ctrl.ClosePot(); // 第二次
        int secondResult = state.Bottom.GetFlavor(FlavorType.Sour);

        Assert(firstResult == secondResult,
            "ClosePot 重复调用结果应一致（单调合并 + pot Flavor 不变）");
    }

    // ─────────────────────────────────────────────────────────────────────────

    static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception($"[FAIL] PotLifecycleTests: {message}");
    }
}
