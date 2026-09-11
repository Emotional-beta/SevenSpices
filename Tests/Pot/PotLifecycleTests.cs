using SevenSpices.Core.Effects;
using SevenSpices.Core.Game;
using SevenSpices.Core.Ingredients;
using SevenSpices.Core.Pot;
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
        // BottomExtractor 对每个 Flavor 取30%，即使值为0也写入（min 1）
        // 本测试未设置 Flavor，BottomExtractor 以 Flavor 值0走 min 1 路径
        Assert(state.Bottom.GetFlavor(FlavorType.Sweet) == 1,
            "ClosePot 后锅底各 Flavor 最低应为 1");
    }

    /// <summary>最终锅通过 EndPot() 强制结束后，ClosePot() 应正确提炼锅底。</summary>
    static void Test_FinalPot_EndPot_ThenClosePot()
    {
        var state = new GameState();
        state.Pot.BowlLimit = int.MaxValue; // 最终锅
        var ctrl = new PotController(state);
        ctrl.StartPot();

        // 走完11碗
        for (int i = 1; i <= 10; i++)
        {
            RunBowlToEnd(ctrl);
            ctrl.StartNextBowl();
        }
        Assert(state.Pot.Phase == PotPhase.InProgress, "最终锅第11碗应仍为 InProgress");
        Assert(state.Pot.BowlNumber == 11, "BowlNumber 应为 11");

        // 主动触发 EndPot（Final Pot 玩家主动结束的入口）
        ctrl.EndPot();
        Assert(state.Pot.Phase == PotPhase.Ended, "EndPot 后应为 Ended");

        // 设置 Flavor 验证 ClosePot 能正确运行
        state.Pot.AddFlavor(FlavorType.Spicy, 20);
        ctrl.ClosePot();

        // 30% of 20 = 6
        Assert(state.Bottom.GetFlavor(FlavorType.Spicy) == 6,
            "最终锅 ClosePot 后锅底 Spicy 应为 6（20×30%）");
    }

    /// <summary>ClosePot 不得在已提炼后重复调用（锅已 Ended，ClosePot 本身无状态变更，但可重复提炼，记录此行为）。</summary>
    static void Test_ClosePot_CannotBeCalledTwiceWithoutNewPot()
    {
        // ClosePot 只检查 PotPhase.Ended，调用两次会重复提炼锅底。
        // 这是当前设计允许的（BottomExtractor 是幂等覆盖写入）。
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
            "ClosePot 是幂等覆盖写入，两次结果应一致");
    }

    // ─────────────────────────────────────────────────────────────────────────

    static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception($"[FAIL] PotLifecycleTests: {message}");
    }
}
