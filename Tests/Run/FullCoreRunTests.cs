using SevenSpices.Core.Content;
using SevenSpices.Core.Effects;
using SevenSpices.Core.Game;
using SevenSpices.Core.Ingredients;
using SevenSpices.Core.Pot;
using SevenSpices.Core.Run;
using SevenSpices.Core.Scoring;

namespace SevenSpices.Tests.Run;

/// <summary>
/// 完整 Core Run 测试：使用正式 IngredientData / CustomerData 数据驱动完整游戏流程。
/// Phase 4 Part 4。
/// </summary>
public static class FullCoreRunTests
{
    public static void RunAll()
    {
        Test_StartRun_WithOfficialData_PotIsInProgress();
        Test_NormalPot_OfficialIngredients_CompletesCorrectly();
        Test_Run_AdvancesChapterAndPotIndex();
        Test_FinalPot_OfficialIngredients_NoCooking_GoldUnchanged();
        Test_FinalPot_EndCooking_NormalCustomer_RunComplete();
        Test_FinalPot_EndCooking_RareCustomer_Satisfied_RunComplete();
        Test_FinalPot_EndCooking_CalledTwice_Throws();
        Test_FullRun_NineNormalPots_ThenFinalPot_RunComplete();

        Console.WriteLine("All FullCoreRunTests passed.");
    }

    // ── 辅助 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// 从正式 IngredientData 创建实例并投入锅。
    /// PotController.AddIngredient 会自动应用 BaseScore 和 Flavors，然后触发 Effects。
    /// </summary>
    static void AddOfficialIngredient(
        PotController ctrl, GameState state,
        IngredientDefinition def, EffectSystem es)
    {
        var inst = IngredientData.CreateInstance(def.Id);
        ctrl.AddIngredient(inst, es);
    }

    /// <summary>快速跑完一整锅（10碗），不加食材，直接走完所有阶段，不调用 ClosePot。</summary>
    static void RunNormalPotToEnd(PotController ctrl)
    {
        for (int bowl = 1; bowl <= 10; bowl++)
        {
            ctrl.StartBowl();
            for (int s = 0; s < 9; s++) ctrl.AdvanceBowlPhase();
            if (bowl < 10) ctrl.StartNextBowl();
        }
    }

    /// <summary>推进 RunController 经过全部9锅普通锅（空锅，不加食材），进入 Final Pot。</summary>
    static void AdvanceToFinalPot(RunController run, GameState state)
    {
        int total = RunController.ChaptersPerRun * RunController.PotsPerChapter;
        for (int i = 0; i < total; i++)
        {
            var ctrl = run.StartCurrentPot();
            RunNormalPotToEnd(ctrl);
            ctrl.ClosePot();
            run.AdvanceToNextPot();
        }
        Assert(state.Run.IsFinalPot, "AdvanceToFinalPot: 应已进入 Final Pot");
    }

    // ── 测试 ─────────────────────────────────────────────────────────────────

    /// <summary>使用正式数据启动 Run，锅处于 InProgress。</summary>
    static void Test_StartRun_WithOfficialData_PotIsInProgress()
    {
        var state = new GameState();
        var run = new RunController(state);
        run.StartRun();
        run.StartCurrentPot();

        Assert(state.Run.Chapter == 1, "StartRun 后 Chapter=1");
        Assert(state.Run.PotIndex == 1, "StartRun 后 PotIndex=1");
        Assert(state.Pot.Phase == PotPhase.InProgress, "StartCurrentPot 后锅应为 InProgress");

        // 验证能够使用正式 Definition 创建 Instance
        var riceInst = IngredientData.CreateInstance("rice");
        Assert(ReferenceEquals(riceInst.Definition, IngredientData.Rice),
            "创建的米饭实例应引用正式 Definition");
    }

    /// <summary>普通锅使用正式食材完成：米饭+糖+辣椒，验证分数计算。</summary>
    static void Test_NormalPot_OfficialIngredients_CompletesCorrectly()
    {
        var state = new GameState();
        var run = new RunController(state);
        run.StartRun();
        var ctrl = run.StartCurrentPot();
        var es = new EffectSystem();

        ctrl.StartBowl();
        ctrl.AdvanceBowlPhase(); // → Customer
        ctrl.AdvanceBowlPhase(); // → ItemPhase
        ctrl.AdvanceBowlPhase(); // → IngredientSelection
        ctrl.AdvanceBowlPhase(); // → IngredientResolve

        // 投入米饭（基础分1, 鲜+1, 无条件效果）
        AddOfficialIngredient(ctrl, state, IngredientData.Rice, es);
        Assert(state.Pot.BaseScore == 1, "米饭入锅后 BaseScore=1");
        Assert(state.Pot.GetFlavor(FlavorType.Umami) == 1, "米饭入锅后鲜=1");

        ctrl.AdvanceBowlPhase(); // → ScoreCalculation
        ctrl.CalculateScore();
        // Bowl1, BaseScore=1, Multiplier=1, FinalScoreMultiplier=1.0 → FinalScore=1
        Assert(state.Pot.FinalScore == 1, "第1碗米饭：FinalScore=1");
        Assert(state.Pot.IsScoreLocked, "分数已锁定");

        // 把第一碗走完
        ctrl.AdvanceBowlPhase(); // → ScoreLocked
        ctrl.AdvanceBowlPhase(); // → Serving
        ctrl.AdvanceBowlPhase(); // → Reward
        ctrl.AdvanceBowlPhase(); // → End

        // 继续剩余9碗（空碗）
        for (int bowl = 2; bowl <= 10; bowl++)
        {
            ctrl.StartNextBowl();
            ctrl.StartBowl();
            for (int s = 0; s < 9; s++) ctrl.AdvanceBowlPhase();
            if (bowl < 10) { /* 第10碗到 End 时锅自动 Ended */ }
        }

        Assert(state.Pot.Phase == PotPhase.Ended, "10碗后锅应 Ended");
        ctrl.ClosePot();
    }

    /// <summary>Run 可以通过正式 API 推进 PotIndex 和 Chapter。</summary>
    static void Test_Run_AdvancesChapterAndPotIndex()
    {
        var state = new GameState();
        var run = new RunController(state);
        run.StartRun();

        // 完成 Chapter 1 的3锅
        for (int i = 0; i < 3; i++)
        {
            var ctrl = run.StartCurrentPot();
            RunNormalPotToEnd(ctrl);
            ctrl.ClosePot();
            run.AdvanceToNextPot();
        }

        Assert(state.Run.Chapter == 2, "完成 Chapter 1 后应进入 Chapter 2");
        Assert(state.Run.PotIndex == 1, "新章节 PotIndex 应重置为 1");
        Assert(!state.Run.IsFinalPot, "仍有普通锅，IsFinalPot 应为 false");
    }

    /// <summary>Final Pot 煮粥期间投入正式食材，金币不增加。</summary>
    static void Test_FinalPot_OfficialIngredients_NoCooking_GoldUnchanged()
    {
        var state = new GameState();
        var run = new RunController(state);
        run.StartRun();
        AdvanceToFinalPot(run, state);

        var ctrl = run.StartCurrentPot();
        var es = new EffectSystem();
        ctrl.StartBowl();
        ctrl.AdvanceBowlPhase(); // → Customer
        ctrl.AdvanceBowlPhase(); // → ItemPhase
        ctrl.AdvanceBowlPhase(); // → IngredientSelection
        ctrl.AdvanceBowlPhase(); // → IngredientResolve

        // 投入正式食材
        AddOfficialIngredient(ctrl, state, IngredientData.Rice, es);
        AddOfficialIngredient(ctrl, state, IngredientData.Sugar, es);

        // 未调用 EndCooking，金币不变
        Assert(state.Player.Gold == 0,
            "最终锅投入食材期间未调用 EndCooking，玩家金币应为 0");
        Assert(state.Customer.CurrentCustomer == null,
            "最终锅投入期间 CustomerState.CurrentCustomer 应为 null");
    }

    /// <summary>Final Pot + EndCooking + 普通食客 → RunComplete。</summary>
    static void Test_FinalPot_EndCooking_NormalCustomer_RunComplete()
    {
        var state = new GameState();
        var run = new RunController(state);
        run.StartRun();
        AdvanceToFinalPot(run, state);

        var ctrl = run.StartCurrentPot();
        var es = new EffectSystem();
        ctrl.StartBowl();
        ctrl.AdvanceBowlPhase(); // → Customer
        ctrl.AdvanceBowlPhase(); // → ItemPhase
        ctrl.AdvanceBowlPhase(); // → IngredientSelection
        ctrl.AdvanceBowlPhase(); // → IngredientResolve

        AddOfficialIngredient(ctrl, state, IngredientData.Rice, es);
        AddOfficialIngredient(ctrl, state, IngredientData.Sugar, es);

        Assert(!run.IsRunComplete, "EndCooking 前 IsRunComplete 应为 false");

        run.EndCooking(CustomerData.CreateNormalInstance(), baseGoldReward: 5);

        Assert(state.Player.Gold == 5, "普通食客 EndCooking 后金币应为 5");
        Assert(state.Pot.Phase == PotPhase.Ended, "EndCooking 后锅应 Ended");
        Assert(run.IsRunComplete, "EndCooking 后 IsRunComplete 应为 true");
        Assert(state.Run.IsFinalPot, "IsFinalPot 保持 true");
    }

    /// <summary>Final Pot + EndCooking + 稀有食客满意 → 奖励食材进入 IngredientBasket。</summary>
    static void Test_FinalPot_EndCooking_RareCustomer_Satisfied_RunComplete()
    {
        var state = new GameState();
        var run = new RunController(state);
        run.StartRun();
        AdvanceToFinalPot(run, state);

        var ctrl = run.StartCurrentPot();
        var es = new EffectSystem();
        ctrl.StartBowl();
        ctrl.AdvanceBowlPhase(); // → Customer
        ctrl.AdvanceBowlPhase(); // → ItemPhase
        ctrl.AdvanceBowlPhase(); // → IngredientSelection
        ctrl.AdvanceBowlPhase(); // → IngredientResolve

        // 让 FinalScore 满足稀有食客条件（ScoreAtLeast=20）
        state.Pot.FinalScore = 20;

        run.EndCooking(
            CustomerData.CreateRareInstance(),
            rewardIngredient: IngredientData.CreateInstance("honey"));

        Assert(state.Player.IngredientBasket.Count == 1,
            "稀有食客满意，奖励食材应进入 IngredientBasket");
        Assert(state.Player.IngredientBasket[0].Definition.Id == "honey",
            "奖励食材应为蜂蜜");
        Assert(run.IsRunComplete, "EndCooking 后 IsRunComplete 应为 true");
    }

    /// <summary>EndCooking 重复调用抛异常，不重复发奖励。</summary>
    static void Test_FinalPot_EndCooking_CalledTwice_Throws()
    {
        var state = new GameState();
        var run = new RunController(state);
        run.StartRun();
        AdvanceToFinalPot(run, state);
        run.StartCurrentPot();

        run.EndCooking(CustomerData.CreateNormalInstance(), baseGoldReward: 5);
        int goldAfterFirst = state.Player.Gold;

        bool threw = false;
        try { run.EndCooking(CustomerData.CreateNormalInstance(), baseGoldReward: 5); }
        catch (InvalidOperationException) { threw = true; }

        Assert(threw, "EndCooking 重复调用应抛 InvalidOperationException");
        Assert(state.Player.Gold == goldAfterFirst, "重复 EndCooking 不应再次发奖励");
    }

    /// <summary>完整 Run：9锅普通锅 + Final Pot → RunComplete。</summary>
    static void Test_FullRun_NineNormalPots_ThenFinalPot_RunComplete()
    {
        var state = new GameState();
        var run = new RunController(state);
        run.StartRun();

        var expected = new (int ch, int pi)[]
        {
            (1,1),(1,2),(1,3),
            (2,1),(2,2),(2,3),
            (3,1),(3,2),(3,3),
        };

        int potNum = 0;
        for (int i = 0; i < expected.Length; i++)
        {
            potNum++;
            var (ch, pi) = expected[i];
            Assert(state.Run.Chapter == ch && state.Run.PotIndex == pi,
                $"锅{potNum}：RunState 应为 ({ch},{pi})");

            var ctrl = run.StartCurrentPot();
            var es = new EffectSystem();

            // 第一碗加入正式食材，其余空碗
            ctrl.StartBowl();
            ctrl.AdvanceBowlPhase(); // → Customer
            ctrl.AdvanceBowlPhase(); // → ItemPhase
            ctrl.AdvanceBowlPhase(); // → IngredientSelection
            ctrl.AdvanceBowlPhase(); // → IngredientResolve
            AddOfficialIngredient(ctrl, state, IngredientData.Rice, es);
            for (int s = 0; s < 5; s++) ctrl.AdvanceBowlPhase(); // → End

            // 剩余碗空碗
            for (int bowl = 2; bowl <= 10; bowl++)
            {
                ctrl.StartNextBowl();
                ctrl.StartBowl();
                for (int s = 0; s < 9; s++) ctrl.AdvanceBowlPhase();
            }

            Assert(state.Pot.Phase == PotPhase.Ended,
                $"锅{potNum}：10碗后应 Ended");
            ctrl.ClosePot();
            run.AdvanceToNextPot();
        }

        Assert(state.Run.IsFinalPot, "9锅后应进入 Final Pot");

        // Final Pot
        var finalCtrl = run.StartCurrentPot();
        Assert(state.Pot.BowlLimit == int.MaxValue, "最终锅 BowlLimit 应为 int.MaxValue");

        var finalEs = new EffectSystem();
        finalCtrl.StartBowl();
        finalCtrl.AdvanceBowlPhase(); // → Customer
        finalCtrl.AdvanceBowlPhase(); // → ItemPhase
        finalCtrl.AdvanceBowlPhase(); // → IngredientSelection
        finalCtrl.AdvanceBowlPhase(); // → IngredientResolve

        // 投入正式食材：米饭 + 蜂蜜
        AddOfficialIngredient(finalCtrl, state, IngredientData.Rice, finalEs);
        AddOfficialIngredient(finalCtrl, state, IngredientData.Honey, finalEs);

        run.EndCooking(CustomerData.CreateNormalInstance(), baseGoldReward: 10);

        Assert(state.Player.Gold == 10, "Final Pot 普通食客金币=10");
        Assert(state.Pot.Phase == PotPhase.Ended, "Final Pot EndCooking 后应 Ended");
        Assert(run.IsRunComplete, "完整 Run 完成后 IsRunComplete 应为 true");
    }

    // ─────────────────────────────────────────────────────────────────────────

    static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception($"[FAIL] FullCoreRunTests: {message}");
    }
}
