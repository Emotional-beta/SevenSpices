using SevenSpices.Core.Customers;
using SevenSpices.Core.Game;
using SevenSpices.Core.Ingredients;
using SevenSpices.Core.Items;
using SevenSpices.Core.Pot;
using SevenSpices.Core.Run;

namespace SevenSpices.Tests.Run;

/// <summary>
/// 最终锅结算闭环测试：EndCooking → CustomerService → 奖励 → ClosePot → RunComplete。
/// Phase 4 Part 3。
/// </summary>
public static class FinalPotSettlementTests
{
    public static void RunAll()
    {
        Test_FinalPot_DuringCooking_GoldUnchanged();
        Test_EndCooking_RequiresFinalPot();
        Test_EndCooking_RequiresInProgress();
        Test_EndCooking_PotBecomesEnded();
        Test_EndCooking_NormalCustomer_GrantsGold();
        Test_EndCooking_RareCustomer_Satisfied_GrantsRewardIngredient();
        Test_EndCooking_RareCustomer_NotSatisfied_NoReward();
        Test_EndCooking_CustomerStateCleared();
        Test_EndCooking_ClosePot_ExtractsBottom();
        Test_EndCooking_RunIsComplete();
        Test_EndCooking_CalledTwice_Throws();
        Test_EndCooking_AdvanceToNextPot_StillBlocked();

        Console.WriteLine("All FinalPotSettlementTests passed.");
    }

    // ── 辅助 ─────────────────────────────────────────────────────────────────

    /// <summary>推进 Run 经过全部9锅普通锅，返回处于 IsFinalPot=true 状态的 RunController。</summary>
    static RunController MakeRunAtFinalPot(GameState state)
    {
        var run = new RunController(state);
        run.StartRun();
        int total = RunController.ChaptersPerRun * RunController.PotsPerChapter;
        for (int i = 0; i < total; i++)
        {
            var ctrl = run.StartCurrentPot();
            RunNormalPotToEnd(ctrl);
            ctrl.ClosePot();
            run.AdvanceToNextPot();
        }
        Assert(state.Run.IsFinalPot, "MakeRunAtFinalPot: 应已进入 Final Pot");
        return run;
    }

    /// <summary>快速跑完一整锅（10碗），不调用 ClosePot。</summary>
    static void RunNormalPotToEnd(PotController ctrl)
    {
        for (int bowl = 1; bowl <= 10; bowl++)
        {
            ctrl.StartBowl();
            for (int s = 0; s < 9; s++) ctrl.AdvanceBowlPhase();
            if (bowl < 10) ctrl.StartNextBowl();
        }
    }

    /// <summary>在最终锅中走几碗但不结束。</summary>
    static void RunFewBowlsInFinalPot(PotController ctrl, int bowls)
    {
        for (int bowl = 0; bowl < bowls; bowl++)
        {
            ctrl.StartBowl();
            for (int s = 0; s < 9; s++) ctrl.AdvanceBowlPhase();
            ctrl.StartNextBowl();
        }
    }

    static CustomerInstance MakeNormalCustomer() =>
        new(new CustomerDefinition("c_normal", "普通食客", isRare: false));

    static CustomerInstance MakeRareCustomer(int scoreThreshold = 10) =>
        new(new CustomerDefinition("c_rare", "稀有食客", isRare: true,
            satisfactionConditions: new[]
            {
                new CustomerSatisfactionCondition(ConditionType.ScoreAtLeast, scoreThreshold)
            }));

    // ── 测试 ─────────────────────────────────────────────────────────────────

    /// <summary>最终锅煮粥期间，玩家金币不会自动增加。</summary>
    static void Test_FinalPot_DuringCooking_GoldUnchanged()
    {
        var state = new GameState();
        var run = MakeRunAtFinalPot(state);
        var ctrl = run.StartCurrentPot();

        // 走3碗，不调用 EndCooking
        RunFewBowlsInFinalPot(ctrl, 3);

        Assert(state.Player.Gold == 0, "最终锅投入食材期间玩家金币不应增加");
        Assert(state.Customer.CurrentCustomer == null,
            "最终锅煮粥期间 CustomerState.CurrentCustomer 应为 null");
    }

    /// <summary>非最终锅时调用 EndCooking 应抛异常。</summary>
    static void Test_EndCooking_RequiresFinalPot()
    {
        var state = new GameState();
        var run = new RunController(state);
        run.StartRun();
        run.StartCurrentPot(); // 普通锅

        bool threw = false;
        try { run.EndCooking(MakeNormalCustomer()); }
        catch (InvalidOperationException) { threw = true; }

        Assert(threw, "普通锅时调用 EndCooking 应抛 InvalidOperationException");
    }

    /// <summary>最终锅未启动时调用 EndCooking 应抛异常。</summary>
    static void Test_EndCooking_RequiresInProgress()
    {
        var state = new GameState();
        var run = MakeRunAtFinalPot(state);
        // 此时 IsFinalPot=true，但锅还没有启动（Phase=NotStarted after Reset 或 Ended after last advance）
        // 直接调用 EndCooking，应因 EndPot 检查而抛异常
        // state.Pot.Phase 此时为 Ended（最后一锅普通锅结束后 AdvanceToNextPot 没有 Reset）
        // 实际是 Ended，所以 EndPot() 会抛
        bool threw = false;
        try { run.EndCooking(MakeNormalCustomer()); }
        catch (InvalidOperationException) { threw = true; }

        Assert(threw, "锅未处于 InProgress 时调用 EndCooking 应抛 InvalidOperationException");
    }

    /// <summary>EndCooking 后锅应为 Ended。</summary>
    static void Test_EndCooking_PotBecomesEnded()
    {
        var state = new GameState();
        var run = MakeRunAtFinalPot(state);
        run.StartCurrentPot();

        run.EndCooking(MakeNormalCustomer());

        Assert(state.Pot.Phase == PotPhase.Ended,
            "EndCooking 后锅应为 PotPhase.Ended");
    }

    /// <summary>普通食客 EndCooking → 金币奖励写入 PlayerState。</summary>
    static void Test_EndCooking_NormalCustomer_GrantsGold()
    {
        var state = new GameState();
        state.Player.Gold = 10;
        var run = MakeRunAtFinalPot(state);
        run.StartCurrentPot();

        run.EndCooking(MakeNormalCustomer(), baseGoldReward: 5);

        Assert(state.Player.Gold == 15,
            "普通食客 EndCooking 后金币应增加 baseGoldReward=5");
    }

    /// <summary>稀有食客满意 → 食材奖励写入 PlayerState.IngredientBasket。</summary>
    static void Test_EndCooking_RareCustomer_Satisfied_GrantsRewardIngredient()
    {
        var state = new GameState();
        var run = MakeRunAtFinalPot(state);
        run.StartCurrentPot();

        // 让 FinalScore 满足稀有食客条件（threshold=10）
        state.Pot.FinalScore = 20;

        var rewardIngr = new IngredientInstance(
            new IngredientDefinition("chili", "辣椒", IngredientRarity.Uncommon, 8));

        run.EndCooking(MakeRareCustomer(scoreThreshold: 10), rewardIngredient: rewardIngr);

        Assert(state.Player.IngredientBasket.Count == 1,
            "稀有食客满意后食材奖励应进入 IngredientBasket");
        Assert(ReferenceEquals(state.Player.IngredientBasket[0], rewardIngr),
            "IngredientBasket[0] 应为传入的 rewardIngredient 实例");
    }

    /// <summary>稀有食客不满意 → 无食材奖励。</summary>
    static void Test_EndCooking_RareCustomer_NotSatisfied_NoReward()
    {
        var state = new GameState();
        var run = MakeRunAtFinalPot(state);
        run.StartCurrentPot();

        // FinalScore=0 < threshold=10 → 不满意
        state.Pot.FinalScore = 0;

        var rewardIngr = new IngredientInstance(
            new IngredientDefinition("rice", "米饭", IngredientRarity.Common, 5));
        var rewardItem = new ItemInstance(new ItemDefinition("sauce", "酱料"));

        run.EndCooking(MakeRareCustomer(scoreThreshold: 10),
            rewardIngredient: rewardIngr, rewardItem: rewardItem);

        Assert(state.Player.IngredientBasket.Count == 0,
            "稀有食客不满意时不应发放食材奖励");
        Assert(state.Player.Items.Count == 0,
            "稀有食客不满意时不应发放道具奖励");
    }

    /// <summary>EndCooking 后 CustomerState.CurrentCustomer 应被清空。</summary>
    static void Test_EndCooking_CustomerStateCleared()
    {
        var state = new GameState();
        var run = MakeRunAtFinalPot(state);
        run.StartCurrentPot();

        run.EndCooking(MakeNormalCustomer());

        Assert(state.Customer.CurrentCustomer == null,
            "EndCooking 后 CustomerState.CurrentCustomer 应为 null");
    }

    /// <summary>EndCooking 调用 ClosePot，锅底被正确提炼。</summary>
    static void Test_EndCooking_ClosePot_ExtractsBottom()
    {
        var state = new GameState();
        var run = MakeRunAtFinalPot(state);
        run.StartCurrentPot();

        state.Pot.AddFlavor(FlavorType.Sweet, 10);

        run.EndCooking(MakeNormalCustomer());

        // 30% of 10 = 3
        Assert(state.Bottom.GetFlavor(FlavorType.Sweet) == 3,
            "EndCooking 后锅底 Sweet 应为 3（10×30%）");
    }

    /// <summary>EndCooking 后 IsRunComplete 为 true。</summary>
    static void Test_EndCooking_RunIsComplete()
    {
        var state = new GameState();
        var run = MakeRunAtFinalPot(state);
        var ctrl = run.StartCurrentPot();

        Assert(!run.IsRunComplete, "EndCooking 前 IsRunComplete 应为 false");

        RunFewBowlsInFinalPot(ctrl, 2);
        run.EndCooking(MakeNormalCustomer(), baseGoldReward: 3);

        Assert(run.IsRunComplete, "EndCooking 后 IsRunComplete 应为 true");
    }

    /// <summary>EndCooking 重复调用应抛异常，不会重复发放奖励。</summary>
    static void Test_EndCooking_CalledTwice_Throws()
    {
        var state = new GameState();
        var run = MakeRunAtFinalPot(state);
        run.StartCurrentPot();

        run.EndCooking(MakeNormalCustomer(), baseGoldReward: 5);
        int goldAfterFirst = state.Player.Gold;

        bool threw = false;
        try { run.EndCooking(MakeNormalCustomer(), baseGoldReward: 5); }
        catch (InvalidOperationException) { threw = true; }

        Assert(threw, "EndCooking 重复调用应抛 InvalidOperationException");
        Assert(state.Player.Gold == goldAfterFirst,
            "重复调用 EndCooking 不应再次发放奖励");
    }

    /// <summary>EndCooking 后 AdvanceToNextPot 仍然被禁止。</summary>
    static void Test_EndCooking_AdvanceToNextPot_StillBlocked()
    {
        var state = new GameState();
        var run = MakeRunAtFinalPot(state);
        run.StartCurrentPot();
        run.EndCooking(MakeNormalCustomer());

        bool threw = false;
        try { run.AdvanceToNextPot(); }
        catch (InvalidOperationException) { threw = true; }

        Assert(threw, "最终锅结算后 AdvanceToNextPot 仍应抛 InvalidOperationException");
    }

    // ─────────────────────────────────────────────────────────────────────────

    static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception($"[FAIL] FinalPotSettlementTests: {message}");
    }
}
