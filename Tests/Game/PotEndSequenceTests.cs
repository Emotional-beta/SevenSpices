using SevenSpices.Core.Companions;
using SevenSpices.Core.Content;
using SevenSpices.Core.Customers;
using SevenSpices.Core.Game;
using SevenSpices.Core.Ingredients;
using SevenSpices.Core.Pot;

namespace SevenSpices.Tests.Game;

/// <summary>
/// 普通锅结束流程「严格串行」回归测试：奖励 → 伙伴 → 商店。
/// 覆盖：锅结束只开放奖励、奖励未处理前不提供伙伴/商店、二者依次处理后才开放商店、
/// 奖励不会被静默跳过、奖励与伙伴都能获得、非法调用抛异常。
/// 固定种子与固定食材篮保证可复现。
/// </summary>
public static class PotEndSequenceTests
{
    public static void RunAll()
    {
        Test_PotEnd_SerialOrder_RewardThenCompanionThenShop();
        Test_Reward_CannotBeSkipped_NoCompanionOrShopBefore();
        Test_RewardAndCompanion_BothGranted();
        Test_SkipCompanion_ThenShop_UnlocksAdvance();

        Console.WriteLine("All PotEndSequenceTests passed.");
    }

    // ── 辅助 ─────────────────────────────────────────────────────────────────

    /// <summary>把食材篮固定为若干米饭，避免随机食材效果干扰流程断言。</summary>
    static void FillBasketWithRice(GameState state, int count)
    {
        for (int i = 0; i < count; i++)
            state.Player.IngredientBasket.Add(new IngredientInstance(IngredientData.Rice));
    }

    static CustomerDefinition MakeRareWithCompanion(CompanionDefinition reward) =>
        new(
            id: $"rare_{reward.Id}",
            name: "测试稀有食客",
            isRare: true,
            satisfactionConditions: new[]
            {
                new CustomerSatisfactionCondition(ConditionType.ScoreAtLeast, threshold: 1)
            },
            companionReward: reward);

    static CustomerAppearanceConfig ConfigWithRare(int[] rareBowls, params CustomerDefinition[] rares) =>
        new()
        {
            RareBowlNumbers = rareBowls,
            RareProbability = 1.0,
            RareCustomers = rares,
        };

    /// <summary>用「能选就选、池空就跳」走到当前普通锅 Ended，但不处理奖励 / 伙伴 / 商店。</summary>
    static void FinishPotWithoutResolving(GameController gc)
    {
        int guard = 0;
        while (gc.Pot.Phase == PotPhase.InProgress && guard++ < 500)
        {
            if (gc.CanSelectIngredient)
                gc.SelectIngredient(gc.CurrentCandidates[0].InstanceId);
            else if (gc.CanSkipBowl)
                gc.SkipBowl();
            else
                break;
        }

        Assert(gc.Pot.Phase == PotPhase.Ended, "FinishPotWithoutResolving: 普通锅应已 Ended");
    }

    /// <summary>
    /// 构造「第 1 碗稀有且满意、锅结束、三道环节均未处理」的控制器。
    /// 使用默认奖励配置（3 候选）与默认商店配置（3 食材 + 2 道具），奖励 / 伙伴 / 商店三态齐全。
    /// </summary>
    static GameController SetupPotEndedWithSatisfiedRare(CompanionDefinition reward, int seed)
    {
        var state = new GameState();
        FillBasketWithRice(state, 3);
        var config = ConfigWithRare(new[] { 1 }, MakeRareWithCompanion(reward));
        var gc = new GameController(state, config, new Random(seed));
        gc.StartNewGame();

        Assert(gc.CurrentCustomer != null && gc.CurrentCustomer.Definition.IsRare,
            "第 1 碗应指派稀有食客");
        gc.SelectIngredient(gc.CurrentCandidates[0].InstanceId);
        Assert(gc.State.Customer.SatisfiedRareCustomers.Count == 1,
            "第 1 碗应记录 1 个满意的稀有食客");

        FinishPotWithoutResolving(gc);
        return gc;
    }

    static void AssertThrows<TException>(Action action, string message) where TException : Exception
    {
        bool threw = false;
        try { action(); }
        catch (TException) { threw = true; }
        Assert(threw, message);
    }

    // ── 测试 ─────────────────────────────────────────────────────────────────

    /// <summary>串行顺序：锅结束只开放奖励；选奖励后开放伙伴；处理伙伴后开放商店；跳商店后解锁推进。</summary>
    static void Test_PotEnd_SerialOrder_RewardThenCompanionThenShop()
    {
        var reward = CompanionData.GenerousGuestCompanion;
        var gc = SetupPotEndedWithSatisfiedRare(reward, seed: 7101);

        // 1) 锅结束：仅奖励可用，伙伴候选已生成但不提供选择，商店已生成但不开放。
        Assert(gc.IsAwaitingReward, "锅结束应首先处于奖励等待态");
        Assert(!gc.IsAwaitingCompanionChoice, "奖励未处理完前不应提供伙伴选择");
        Assert(gc.CompanionCandidates.Count == 1, "伙伴候选应已生成（只是暂不提供选择）");
        Assert(!gc.IsShopOpen, "奖励未处理完前商店不应开放");
        Assert(gc.ShopOffers.Count > 0, "商店报价应已生成（只是暂不开放）");
        Assert(!gc.CanAdvanceToNextPot, "三道环节未处理完前不应可推进");

        // 奖励未处理时，伙伴 / 商店接口都不可用。
        AssertThrows<InvalidOperationException>(
            () => gc.ChooseCompanion(reward.Id), "奖励未处理时 ChooseCompanion 应抛异常");
        AssertThrows<InvalidOperationException>(
            () => gc.SkipCompanionChoice(), "奖励未处理时 SkipCompanionChoice 应抛异常");
        AssertThrows<InvalidOperationException>(
            () => gc.SkipShop(), "奖励未处理时 SkipShop 应抛异常");

        // 2) 选奖励 → 伙伴开放，商店仍不开。
        gc.ChooseReward(gc.RewardCandidates[0].InstanceId);
        Assert(!gc.IsAwaitingReward, "选定奖励后不应再处于奖励态");
        Assert(gc.IsAwaitingCompanionChoice, "奖励处理后应进入伙伴等待态");
        Assert(!gc.IsShopOpen, "伙伴未处理完前商店不应开放");
        Assert(!gc.CanAdvanceToNextPot, "伙伴未处理完前不应可推进");

        // 3) 处理伙伴 → 商店开放。
        gc.ChooseCompanion(reward.Id);
        Assert(!gc.IsAwaitingCompanionChoice, "选定伙伴后不应再处于伙伴等待态");
        Assert(gc.IsShopOpen, "奖励与伙伴都处理后商店应开放");
        Assert(!gc.CanAdvanceToNextPot, "商店未结算前不应可推进");

        // 4) 跳商店 → 解锁推进。
        gc.SkipShop();
        Assert(!gc.IsShopOpen, "跳过商店后不应再开放");
        Assert(gc.CanAdvanceToNextPot, "三道环节都处理后应可推进");
    }

    /// <summary>奖励不会被跳过：奖励未选定时不可能进入伙伴 / 商店步骤，奖励态原封不动。</summary>
    static void Test_Reward_CannotBeSkipped_NoCompanionOrShopBefore()
    {
        var reward = CompanionData.GenerousGuestCompanion;
        var gc = SetupPotEndedWithSatisfiedRare(reward, seed: 7102);

        int rewardCountBefore = gc.RewardCandidates.Count;
        int basketBefore = gc.Player.IngredientBasket.Count;

        Assert(!gc.IsAwaitingCompanionChoice, "奖励未处理前不得提供伙伴选择");
        Assert(!gc.IsShopOpen, "奖励未处理前不得开放商店");
        Assert(!gc.CanAdvanceToNextPot, "奖励未处理前不得推进");

        AssertThrows<InvalidOperationException>(
            () => gc.ChooseCompanion(reward.Id), "绕过奖励选伙伴应被拒绝");
        AssertThrows<InvalidOperationException>(
            () => gc.SkipCompanionChoice(), "绕过奖励跳过伙伴应被拒绝");
        AssertThrows<InvalidOperationException>(
            () => gc.SkipShop(), "绕过奖励跳过商店应被拒绝");

        Assert(gc.IsAwaitingReward, "以上非法调用后仍应停留在奖励态");
        Assert(gc.RewardCandidates.Count == rewardCountBefore, "非法调用不应消耗奖励候选");
        Assert(gc.Player.IngredientBasket.Count == basketBefore, "非法调用不应改变食材篮");
        Assert(gc.Player.Companions.Count == 0, "奖励未处理前不应获得任何伙伴");
    }

    /// <summary>两道奖励都能获得：食材进篮、伙伴进入 Player.Companions。</summary>
    static void Test_RewardAndCompanion_BothGranted()
    {
        var reward = CompanionData.GenerousGuestCompanion;
        var gc = SetupPotEndedWithSatisfiedRare(reward, seed: 7103);

        int basketBefore = gc.Player.IngredientBasket.Count;
        var chosen = gc.RewardCandidates[0];

        gc.ChooseReward(chosen.InstanceId);
        Assert(gc.Player.IngredientBasket.Count == basketBefore + 1, "食材奖励应使食材篮 +1");
        Assert(gc.Player.IngredientBasket.Contains(chosen), "选中的食材奖励应进入食材篮");

        gc.ChooseCompanion(reward.Id);
        Assert(gc.Player.Companions.Count == 1, "选定的伙伴应进入 Player.Companions");
        Assert(ReferenceEquals(gc.Player.Companions[0].Definition, reward), "持有的应为选定的伙伴");
    }

    /// <summary>跳过伙伴后商店才开放；跳过伙伴同样能走完串行流程。</summary>
    static void Test_SkipCompanion_ThenShop_UnlocksAdvance()
    {
        var reward = CompanionData.GenerousGuestCompanion;
        var gc = SetupPotEndedWithSatisfiedRare(reward, seed: 7104);

        gc.ChooseReward(gc.RewardCandidates[0].InstanceId);
        Assert(gc.IsAwaitingCompanionChoice, "奖励处理后应进入伙伴等待态");

        gc.SkipCompanionChoice();
        Assert(gc.Player.Companions.Count == 0, "跳过伙伴不应获得伙伴");
        Assert(gc.IsShopOpen, "跳过伙伴后商店应开放");

        gc.SkipShop();
        Assert(gc.CanAdvanceToNextPot, "跳过伙伴与商店后应可推进");
    }

    static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception($"[FAIL] PotEndSequenceTests: {message}");
    }
}
