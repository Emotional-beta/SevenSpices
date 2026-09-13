using SevenSpices.Core.Companions;
using SevenSpices.Core.Content;
using SevenSpices.Core.Customers;
using SevenSpices.Core.Effects;
using SevenSpices.Core.Game;
using SevenSpices.Core.Ingredients;
using SevenSpices.Core.Items;
using SevenSpices.Core.Pot;
using SevenSpices.Core.Run;
using SevenSpices.Core.Scoring;

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
        Test_FinalPot_Multiplier_Is32();
        Test_FinalPot_EffectMultiplier_StacksOn32();
        Test_FinalPot_RealIngredients_AccumulateThenSettleAt32();
        Test_FinalPot_BaseScore_AccumulatesAcrossStartBowl();
        Test_FinalPot_StartNextBowl_Throws();
        Test_FinalPot_EndCooking_WhenScoreAlreadyLocked_LeavesPotInProgress();
        Test_EndCooking_AppliesBottomSettlementHook_E2();

        Console.WriteLine("All FinalPotSettlementTests passed.");
    }

    // ── 辅助 ─────────────────────────────────────────────────────────────────

    /// <summary>推进 Run 经过全部9锅普通锅，返回处于 IsFinalPot=true 状态的 RunController。</summary>
    static RunController MakeRunAtFinalPot(GameState state, CompanionSystem? companions = null)
    {
        var run = new RunController(state, companions);
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

    /// <summary>在最终锅中开一碗并推进到 IngredientResolve（模拟持续投入食材，不结算）。</summary>
    static void StartFinalPotBowl(PotController ctrl)
    {
        ctrl.StartBowl();
        for (int s = 0; s < 4; s++) ctrl.AdvanceBowlPhase(); // → IngredientResolve
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

        // 持续投入食材，不调用 EndCooking
        StartFinalPotBowl(ctrl);
        state.Pot.AddFlavor(FlavorType.Sweet, 10);
        state.Pot.BaseScore = 50;

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

        // 让 BaseScore 满足稀有食客条件（threshold=10）。
        // 最终锅不再手工写 FinalScore：EndCooking 会按 BaseScore × 32 计算并锁定，
        // 1 × 32 = 32 ≥ 10 → 满意。这同时验证了「结算前自动算分」的缺口已修复。
        state.Pot.BaseScore = 1;

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

        // BaseScore 保持 0 → EndCooking 算出 FinalScore = 0 × 32 = 0 < threshold=10 → 不满意
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

    /// <summary>
    /// 最终锅整链 RunController.EndCooking → ClosePot → E2：
    /// 装备「甜心老板」后，锅底提炼结果（甜 10 → 3）再被 E2 提升最高味道 +2 → 5。
    /// </summary>
    static void Test_EndCooking_AppliesBottomSettlementHook_E2()
    {
        var state = new GameState();
        // CompanionSystem 复用 PlayerState.Companions 的同一列表，先装备再建 RunController。
        var companions = new CompanionSystem(state.Player.Companions);
        state.Player.Companions.Add(new CompanionInstance(CompanionData.SweetBossCompanion));

        var run = MakeRunAtFinalPot(state, companions);
        run.StartCurrentPot();

        state.Pot.AddFlavor(FlavorType.Sweet, 10);

        run.EndCooking(MakeNormalCustomer());

        Assert(state.Bottom.GetFlavor(FlavorType.Sweet) == 5,
            $"甜心老板 E2 应把最终锅锅底最高味道 3 提升到 5，实际 {state.Bottom.GetFlavor(FlavorType.Sweet)}");
    }

    /// <summary>EndCooking 后 IsRunComplete 为 true。</summary>
    static void Test_EndCooking_RunIsComplete()
    {
        var state = new GameState();
        var run = MakeRunAtFinalPot(state);
        var ctrl = run.StartCurrentPot();

        Assert(!run.IsRunComplete, "EndCooking 前 IsRunComplete 应为 false");

        StartFinalPotBowl(ctrl);
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
    // 最终锅一次性结算：固定 ×32、分数跨碗累积、禁止进入下一碗

    /// <summary>最终锅结算倍率固定为 ×32。</summary>
    static void Test_FinalPot_Multiplier_Is32()
    {
        var state = new GameState();
        var run = MakeRunAtFinalPot(state);
        run.StartCurrentPot();

        state.Pot.BaseScore = 100;
        run.EndCooking(MakeNormalCustomer());

        Assert(state.Pot.FinalScore == 3200,
            "最终锅 BaseScore=100 应按 ×32 结算为 3200");
        Assert(state.Pot.IsScoreLocked, "最终锅结算后分数应已锁定");
    }

    /// <summary>最终锅的 ×32 之后仍叠加效果倍率（Q2 裁定）。</summary>
    static void Test_FinalPot_EffectMultiplier_StacksOn32()
    {
        var state = new GameState();
        var run = MakeRunAtFinalPot(state);
        run.StartCurrentPot();

        state.Pot.BaseScore = 100;
        state.Pot.FinalScoreMultiplier = 1.5; // 冰块等效果写入
        run.EndCooking(MakeNormalCustomer());

        Assert(state.Pot.FinalScore == 4800,
            "最终锅 FinalScore 应为 floor(100 × 32 × 1.5) = 4800");
    }

    /// <summary>真实路径：在最终锅反复投入正式食材，EndCooking 后整锅按 ×32 一次性结算。</summary>
    static void Test_FinalPot_RealIngredients_AccumulateThenSettleAt32()
    {
        var state = new GameState();
        var run = MakeRunAtFinalPot(state);
        var ctrl = run.StartCurrentPot();
        var es = new EffectSystem();

        StartFinalPotBowl(ctrl);
        ctrl.AddIngredient(IngredientData.CreateInstance("rice"), es);   // 基础分 1
        ctrl.AddIngredient(IngredientData.CreateInstance("sugar"), es);  // 基础分 2

        int accumulated = state.Pot.BaseScore;
        Assert(accumulated == 3, "米饭+糖应累积 BaseScore=3");
        Assert(state.Pot.FinalScore == 0, "结算前最终锅不应逐碗锁分，FinalScore 仍为 0");

        // 结算用「累积基础分」含味道分：米饭鲜+1 + 糖甜+1 = 味道分 2。
        int totalBase = (int)Math.Floor(state.Pot.BaseScoreWithFlavor);
        run.EndCooking(MakeNormalCustomer());

        Assert(state.Pot.FinalScore == totalBase * 32,
            $"最终锅应按「基础分{accumulated} + 味道分2 = {totalBase}」× 32 一次性结算");
    }

    /// <summary>最终锅 StartingBowl 不再清零 BaseScore，整锅分数累积成「一大碗」。</summary>
    static void Test_FinalPot_BaseScore_AccumulatesAcrossStartBowl()
    {
        var state = new GameState();
        var run = MakeRunAtFinalPot(state);
        var ctrl = run.StartCurrentPot();

        ctrl.StartBowl();
        state.Pot.BaseScore = 7;
        state.Pot.FinalScore = 99;
        ctrl.StartBowl(); // 最终锅不应清零

        Assert(state.Pot.BaseScore == 7,
            "最终锅 StartBowl 不应重置 BaseScore，分数必须跨阶段累积");
        Assert(state.Pot.FinalScore == 99,
            "最终锅 StartBowl 不应重置 FinalScore");
    }

    /// <summary>最终锅禁止进入下一碗。</summary>
    static void Test_FinalPot_StartNextBowl_Throws()
    {
        var state = new GameState();
        var run = MakeRunAtFinalPot(state);
        var ctrl = run.StartCurrentPot();

        ctrl.StartBowl();
        for (int s = 0; s < 9; s++) ctrl.AdvanceBowlPhase(); // → End

        bool threw = false;
        try { ctrl.StartNextBowl(); }
        catch (InvalidOperationException) { threw = true; }

        Assert(threw, "最终锅调用 StartNextBowl 应抛 InvalidOperationException");
        Assert(state.Pot.BowlNumber == ScoreCalculator.FinalPotBowlNumber,
            "最终锅 BowlNumber 应保持 ×32 档位不变");
    }

    /// <summary>
    /// 分数已被锁定时 EndCooking 必须零副作用地失败：锅保持 InProgress（可重试），
    /// 不能留下「锅已 Ended 但奖励未发」且无法重试的死局。
    /// </summary>
    static void Test_FinalPot_EndCooking_WhenScoreAlreadyLocked_LeavesPotInProgress()
    {
        var state = new GameState();
        var run = MakeRunAtFinalPot(state);
        run.StartCurrentPot();

        state.Pot.BaseScore = 10;
        ScoreCalculator.CalculateAndLock(state.Pot); // 模拟提前锁分

        bool threw = false;
        try { run.EndCooking(MakeNormalCustomer(), baseGoldReward: 5); }
        catch (InvalidOperationException) { threw = true; }

        Assert(threw, "分数已锁定时 EndCooking 应抛 InvalidOperationException");
        Assert(state.Pot.Phase == PotPhase.InProgress,
            "结算失败时锅必须保持 InProgress，不得留下半结算状态");
        Assert(state.Player.Gold == 0, "结算失败时不应发放奖励");
        Assert(state.Customer.CurrentCustomer == null, "结算失败时不应指派食客");
    }

    // ─────────────────────────────────────────────────────────────────────────

    static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception($"[FAIL] FinalPotSettlementTests: {message}");
    }
}
