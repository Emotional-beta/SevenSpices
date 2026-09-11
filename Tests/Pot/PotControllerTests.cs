using SevenSpices.Core.Game;
using SevenSpices.Core.Pot;

namespace SevenSpices.Tests.Pot;

/// <summary>
/// PotController 基础流程测试。
/// 覆盖：开锅初始化、碗的开始、碗数递增、普通锅第10碗结束、第11碗禁止、End状态阻止推进、最终锅边界。
/// </summary>
public static class PotControllerTests
{
    public static void RunAll()
    {
        Test_StartPot_InitializesCorrectly();
        Test_StartBowl_SetsPhaseAndResetsScore();
        Test_AdvanceBowlPhase_ProgressesThroughAllPhases();
        Test_BowlNumber_IncrementsOnStartNextBowl();
        Test_NormalPot_EndsAfterTenthBowl();
        Test_NormalPot_CannotStartEleventhBowl();
        Test_EndedPot_CannotAdvanceBowlPhase();
        Test_EndedPot_CannotStartBowl();
        Test_FinalPot_DoesNotEndAfterTenthBowl();
        Test_FinalPot_CanStartEleventhBowl();
        Test_StartPot_CannotBeCalledTwice();
        Test_AdvanceBowlPhase_ThrowsWhenAtEnd();

        Console.WriteLine("All PotControllerTests passed.");
    }

    // ── 辅助方法 ─────────────────────────────────────────────────────────────

    /// <summary>将当前碗从 Start 阶段一路推进到 End 阶段（9次 AdvanceBowlPhase）。</summary>
    static void AdvanceCurrentBowlToEnd(PotController controller)
    {
        // BowlPhase.Start(0) → BowlPhase.End(9)，共 9 步
        int steps = (int)BowlPhase.End - (int)BowlPhase.Start;
        for (int i = 0; i < steps; i++)
            controller.AdvanceBowlPhase();
    }

    /// <summary>完整地走完一碗（StartBowl + 推进到 End），不推进到下一碗。</summary>
    static void RunBowlToEnd(PotController controller)
    {
        controller.StartBowl();
        AdvanceCurrentBowlToEnd(controller);
    }

    // ── 测试用例 ──────────────────────────────────────────────────────────────

    static void Test_StartPot_InitializesCorrectly()
    {
        var state = new GameState();
        var controller = new PotController(state);
        controller.StartPot();

        Assert(state.Pot.Phase == PotPhase.InProgress, "Pot phase should be InProgress after StartPot");
        Assert(state.Pot.BowlNumber == 1, "BowlNumber should be 1 after StartPot");
        Assert(state.Pot.BowlLimit == 10, "BowlLimit should be 10 by default (normal pot)");
    }

    static void Test_StartBowl_SetsPhaseAndResetsScore()
    {
        var state = new GameState();
        var controller = new PotController(state);
        controller.StartPot();

        // 人为设置一些"上一碗"残留数据，验证 StartBowl 会清零
        state.Pot.BaseScore = 99;
        state.Pot.FinalScore = 200;
        state.Pot.IsScoreLocked = true;

        controller.StartBowl();

        Assert(state.Pot.CurrentBowlPhase == BowlPhase.Start, "CurrentBowlPhase should be Start");
        Assert(state.Pot.BaseScore == 0, "BaseScore should reset to 0");
        Assert(state.Pot.FinalScore == 0, "FinalScore should reset to 0");
        Assert(!state.Pot.IsScoreLocked, "IsScoreLocked should reset to false");
    }

    static void Test_AdvanceBowlPhase_ProgressesThroughAllPhases()
    {
        var state = new GameState();
        var controller = new PotController(state);
        controller.StartPot();
        controller.StartBowl();

        // 逐步推进并验证每个阶段
        var expectedSequence = new[]
        {
            BowlPhase.Customer,
            BowlPhase.ItemPhase,
            BowlPhase.IngredientSelection,
            BowlPhase.IngredientResolve,
            BowlPhase.ScoreCalculation,
            BowlPhase.ScoreLocked,
            BowlPhase.Serving,
            BowlPhase.Reward,
            BowlPhase.End
        };

        foreach (var expected in expectedSequence)
        {
            controller.AdvanceBowlPhase();
            Assert(state.Pot.CurrentBowlPhase == expected, $"Expected phase {expected}, got {state.Pot.CurrentBowlPhase}");
        }
    }

    static void Test_BowlNumber_IncrementsOnStartNextBowl()
    {
        var state = new GameState();
        var controller = new PotController(state);
        controller.StartPot();

        RunBowlToEnd(controller);
        Assert(state.Pot.BowlNumber == 1, "BowlNumber should still be 1 at End of bowl 1");

        controller.StartNextBowl();
        Assert(state.Pot.BowlNumber == 2, "BowlNumber should be 2 after StartNextBowl");
        Assert(state.Pot.CurrentBowlPhase == BowlPhase.Start, "CurrentBowlPhase should reset to Start for bowl 2");
        Assert(state.Pot.Phase == PotPhase.InProgress, "Pot should still be InProgress");

        AdvanceCurrentBowlToEnd(controller);
        controller.StartNextBowl();
        Assert(state.Pot.BowlNumber == 3, "BowlNumber should be 3");
    }

    static void Test_NormalPot_EndsAfterTenthBowl()
    {
        var state = new GameState();
        var controller = new PotController(state);
        controller.StartPot();

        // 前9碗：正常走完后推进到下一碗
        for (int i = 1; i <= 9; i++)
        {
            RunBowlToEnd(controller);
            Assert(state.Pot.Phase == PotPhase.InProgress, $"Pot should be InProgress after bowl {i} ends");
            controller.StartNextBowl();
            Assert(state.Pot.BowlNumber == i + 1, $"BowlNumber should be {i + 1}");
        }

        // 第10碗走到 End，锅应自动结束
        Assert(state.Pot.BowlNumber == 10, "Should be bowl 10");
        RunBowlToEnd(controller);

        Assert(state.Pot.Phase == PotPhase.Ended, "Pot should be Ended after 10th bowl ends");
        Assert(state.Pot.BowlNumber == 10, "BowlNumber should remain 10");
        Assert(state.Pot.CurrentBowlPhase == BowlPhase.End, "CurrentBowlPhase should be End");
    }

    static void Test_NormalPot_CannotStartEleventhBowl()
    {
        var state = new GameState();
        var controller = new PotController(state);
        controller.StartPot();

        for (int i = 1; i <= 9; i++)
        {
            RunBowlToEnd(controller);
            controller.StartNextBowl();
        }
        RunBowlToEnd(controller); // 第10碗结束，锅进入 Ended

        Assert(state.Pot.Phase == PotPhase.Ended, "Pot should be Ended before asserting 11th bowl is blocked");

        bool threw = false;
        try { controller.StartNextBowl(); }
        catch (InvalidOperationException) { threw = true; }

        Assert(threw, "StartNextBowl on an Ended pot should throw InvalidOperationException");
    }

    static void Test_EndedPot_CannotAdvanceBowlPhase()
    {
        var state = new GameState();
        var controller = new PotController(state);
        controller.StartPot();

        for (int i = 1; i <= 9; i++)
        {
            RunBowlToEnd(controller);
            controller.StartNextBowl();
        }
        RunBowlToEnd(controller); // 第10碗结束，锅 Ended

        // 锅已结束后，尝试推进阶段应抛异常
        // 注意：此时 CurrentBowlPhase == End，即使改为 Start 后也不能推进
        state.Pot.CurrentBowlPhase = BowlPhase.Start; // 模拟绕过检查

        bool threw = false;
        try { controller.AdvanceBowlPhase(); }
        catch (InvalidOperationException) { threw = true; }

        Assert(threw, "AdvanceBowlPhase on an Ended pot should throw InvalidOperationException");
    }

    static void Test_EndedPot_CannotStartBowl()
    {
        var state = new GameState();
        var controller = new PotController(state);
        controller.StartPot();

        for (int i = 1; i <= 9; i++)
        {
            RunBowlToEnd(controller);
            controller.StartNextBowl();
        }
        RunBowlToEnd(controller); // 第10碗结束

        bool threw = false;
        try { controller.StartBowl(); }
        catch (InvalidOperationException) { threw = true; }

        Assert(threw, "StartBowl on an Ended pot should throw InvalidOperationException");
    }

    static void Test_FinalPot_DoesNotEndAfterTenthBowl()
    {
        var state = new GameState();
        // 最终锅：BowlLimit 设为极大值
        state.Pot.BowlLimit = int.MaxValue;

        var controller = new PotController(state);
        controller.StartPot();

        // 走完10碗
        for (int i = 1; i <= 9; i++)
        {
            RunBowlToEnd(controller);
            controller.StartNextBowl();
        }
        RunBowlToEnd(controller); // 第10碗结束

        // 最终锅不应该结束
        Assert(state.Pot.Phase == PotPhase.InProgress, "Final pot should still be InProgress after bowl 10");
        Assert(state.Pot.BowlNumber == 10, "BowlNumber should be 10");
    }

    static void Test_FinalPot_CanStartEleventhBowl()
    {
        var state = new GameState();
        state.Pot.BowlLimit = int.MaxValue;

        var controller = new PotController(state);
        controller.StartPot();

        for (int i = 1; i <= 9; i++)
        {
            RunBowlToEnd(controller);
            controller.StartNextBowl();
        }
        RunBowlToEnd(controller); // 第10碗结束

        // 最终锅可以进入第11碗
        controller.StartNextBowl();
        Assert(state.Pot.BowlNumber == 11, "Final pot should allow bowl 11");
        Assert(state.Pot.CurrentBowlPhase == BowlPhase.Start, "Bowl 11 should start at BowlPhase.Start");
        Assert(state.Pot.Phase == PotPhase.InProgress, "Final pot should still be InProgress at bowl 11");
    }

    static void Test_StartPot_CannotBeCalledTwice()
    {
        var state = new GameState();
        var controller = new PotController(state);
        controller.StartPot();

        bool threw = false;
        try { controller.StartPot(); }
        catch (InvalidOperationException) { threw = true; }

        Assert(threw, "StartPot called twice should throw InvalidOperationException");
    }

    static void Test_AdvanceBowlPhase_ThrowsWhenAtEnd()
    {
        var state = new GameState();
        var controller = new PotController(state);
        controller.StartPot();
        RunBowlToEnd(controller); // bowl 1 → End (pot 未结束，BowlNumber=1 < 10)

        // 第1碗结束后锅仍 InProgress，但碗在 End 阶段，推进应抛异常
        Assert(state.Pot.Phase == PotPhase.InProgress, "Pot should be InProgress after bowl 1");
        Assert(state.Pot.CurrentBowlPhase == BowlPhase.End, "Bowl phase should be End");

        bool threw = false;
        try { controller.AdvanceBowlPhase(); }
        catch (InvalidOperationException) { threw = true; }

        Assert(threw, "AdvanceBowlPhase at BowlPhase.End should throw InvalidOperationException");
    }

    // ─────────────────────────────────────────────────────────────────────────

    static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception($"[FAIL] PotControllerTests: {message}");
    }
}
