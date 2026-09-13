using SevenSpices.Core.Companions;
using SevenSpices.Core.Content;
using SevenSpices.Core.Customers;
using SevenSpices.Core.Events;
using SevenSpices.Core.Game;
using SevenSpices.Core.Ingredients;
using SevenSpices.Core.Pot;

namespace SevenSpices.Tests.Game;

/// <summary>
/// 锅结束伙伴候选选择流程测试。
/// 覆盖：候选生成与门控、ChooseCompanion 立即生效、SkipCompanionChoice、无候选自动放行、
/// 候选去重、事件发布、非法调用。
/// 全部使用固定种子与固定食材篮，保证可复现。
/// </summary>
public static class CompanionFlowTests
{
    public static void RunAll()
    {
        Test_PotEnd_WithSatisfiedRare_OffersCandidates_AndBlocksAdvance();
        Test_ChooseCompanion_AddsInstance_IsImmediatelyActive_AndUnlocksAdvance();
        Test_SkipCompanionChoice_UnlocksAdvance_NoCompanionAdded();
        Test_SkipCompanionChoice_PublishesSkippedEvent();
        Test_NoCandidates_AutoResolved_CanAdvance();
        Test_ChooseCompanion_PublishesCompanionAddedEvent();
        Test_DuplicateRewards_AreDeduplicated();
        Test_ChooseCompanion_Invalid_Throws();
        Test_SkipCompanionChoice_Invalid_Throws();
        Test_PreviewIngredient_WithGenerousGuest_AddsOne();

        Console.WriteLine("All CompanionFlowTests passed.");
    }

    // ── 辅助 ─────────────────────────────────────────────────────────────────

    /// <summary>把食材篮固定为若干米饭，避免随机食材效果干扰分数断言。</summary>
    static void FillBasketWithRice(GameState state, int count)
    {
        for (int i = 0; i < count; i++)
            state.Player.IngredientBasket.Add(new IngredientInstance(IngredientData.Rice));
    }

    /// <summary>关闭商店（陈列数量为 0），使伙伴流程测试不受商店门控影响。</summary>
    static ShopConfig NoShop() =>
        new() { IngredientOfferCount = 0, ItemOfferCount = 0 };

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

    /// <summary>用「能选就选、池空就跳」走到当前普通锅 Ended，并选定第一个奖励（但不处理伙伴候选）。</summary>
    static void FinishPotResolveReward(GameController gc)
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

        Assert(gc.Pot.Phase == PotPhase.Ended, "FinishPotResolveReward: 普通锅应已 Ended");

        if (gc.IsAwaitingReward)
            gc.ChooseReward(gc.RewardCandidates[0].InstanceId);
    }

    /// <summary>构造「第 1 碗稀有且满意、锅结束」的控制器，返回控制器与绑定的伙伴。</summary>
    static GameController SetupPotWithSatisfiedRare(
        CompanionDefinition reward, out GameState state, int seed)
    {
        state = new GameState();
        FillBasketWithRice(state, 3);
        var config = ConfigWithRare(new[] { 1 }, MakeRareWithCompanion(reward));
        var gc = new GameController(state, config, new Random(seed), shop: NoShop());
        gc.StartNewGame();

        Assert(gc.CurrentCustomer != null && gc.CurrentCustomer.Definition.IsRare,
            "第 1 碗应指派稀有食客");
        gc.SelectIngredient(gc.CurrentCandidates[0].InstanceId);
        Assert(gc.State.Customer.SatisfiedRareCustomers.Count == 1,
            "第 1 碗应记录 1 个满意的稀有食客");

        FinishPotResolveReward(gc);
        return gc;
    }

    // ── 测试 ─────────────────────────────────────────────────────────────────

    /// <summary>6a. 本锅有满意的稀有食客 → 生成伙伴候选、门控推进。</summary>
    static void Test_PotEnd_WithSatisfiedRare_OffersCandidates_AndBlocksAdvance()
    {
        var reward = CompanionData.GenerousGuestCompanion;
        var gc = SetupPotWithSatisfiedRare(reward, out _, seed: 7001);

        Assert(gc.IsAwaitingCompanionChoice, "锅结束且未处理时应处于伙伴候选等待态");
        Assert(gc.CanChooseCompanion, "CanChooseCompanion 应为 true");
        Assert(gc.CanSkipCompanionChoice, "CanSkipCompanionChoice 应为 true");
        Assert(gc.CompanionCandidates.Count == 1, $"应有 1 个伙伴候选，实际 {gc.CompanionCandidates.Count}");
        Assert(ReferenceEquals(gc.CompanionCandidates[0], reward), "候选应为绑定的豪爽客");
        Assert(!gc.CanAdvanceToNextPot, "未处理伙伴候选前不应可推进");
    }

    /// <summary>6b. ChooseCompanion：进入 PlayerState.Companions、立即生效、解锁推进。</summary>
    static void Test_ChooseCompanion_AddsInstance_IsImmediatelyActive_AndUnlocksAdvance()
    {
        var reward = CompanionData.GenerousGuestCompanion;
        var gc = SetupPotWithSatisfiedRare(reward, out _, seed: 7002);

        gc.ChooseCompanion(reward.Id);

        Assert(gc.Player.Companions.Count == 1, "选定后应持有 1 个伙伴");
        Assert(ReferenceEquals(gc.Player.Companions[0].Definition, reward), "持有的应为选定的豪爽客");
        Assert(gc.CompanionCandidates.Count == 0, "选定后候选应清空");
        Assert(!gc.IsAwaitingCompanionChoice, "选定后不应再处于等待态");
        Assert(gc.CanAdvanceToNextPot, "选定后应可推进到下一锅");

        // 立即生效：CompanionSystem 复用同一列表
        var system = new CompanionSystem(gc.Player.Companions);
        Assert(system.ModifyIngredientBaseScore(IngredientData.CreateInstance("rice"), 1) == 2,
            "选定后同一列表上的 CompanionSystem 应立即生效（米饭 1 → 2）");

        // 通过 GameController 进入下一锅后，实际入锅基础分应 +1
        gc.AdvanceToNextPot();
        Assert(gc.Run.PotIndex == 2, "应进入第 2 锅");

        // 选定后本碗会立刻结算并进入下一碗（BaseScore 被 StartBowl 归零），
        // 因此读取跨碗累计的 TotalBaseScore 来验证伙伴 E1 在下一锅真实生效。
        var first = gc.CurrentCandidates[0];
        int expected = first.Definition.BaseScore + 1;
        gc.SelectIngredient(first.InstanceId);
        Assert(gc.Pot.TotalBaseScore == expected,
            $"第 2 锅首个食材应累计为基础分 + 1（{expected}），实际 {gc.Pot.TotalBaseScore}");
    }

    /// <summary>6c. SkipCompanionChoice：不获得伙伴，同样解锁推进。</summary>
    static void Test_SkipCompanionChoice_UnlocksAdvance_NoCompanionAdded()
    {
        var reward = CompanionData.GenerousGuestCompanion;
        var gc = SetupPotWithSatisfiedRare(reward, out _, seed: 7003);

        gc.SkipCompanionChoice();

        Assert(gc.Player.Companions.Count == 0, "跳过后不应获得伙伴");
        Assert(gc.CompanionCandidates.Count == 0, "跳过后候选应清空");
        Assert(!gc.IsAwaitingCompanionChoice, "跳过后不应再处于等待态");
        Assert(gc.CanAdvanceToNextPot, "跳过后应可推进到下一锅");
    }

    /// <summary>6c-b. SkipCompanionChoice 发布 CompanionChoiceSkippedEvent，负载为跳过时的候选数。</summary>
    static void Test_SkipCompanionChoice_PublishesSkippedEvent()
    {
        var reward = CompanionData.GenerousGuestCompanion;
        var gc = SetupPotWithSatisfiedRare(reward, out _, seed: 7010);

        CompanionChoiceSkippedEvent? skipped = null;
        gc.Events.Subscribe<CompanionChoiceSkippedEvent>(e => skipped = e);

        int candidatesBefore = gc.CompanionCandidates.Count;
        gc.SkipCompanionChoice();

        Assert(skipped != null, "SkipCompanionChoice 应发布 CompanionChoiceSkippedEvent");
        Assert(skipped!.CandidateCount == candidatesBefore,
            $"CompanionChoiceSkippedEvent.CandidateCount 应为跳过时的候选数（{candidatesBefore}），实际 {skipped.CandidateCount}");
        Assert(gc.CanAdvanceToNextPot, "跳过伙伴选择后 CanAdvanceToNextPot 应为 true");
    }

    /// <summary>6d. 无候选（本锅无满意稀有食客）→ 自动 resolved，可推进。</summary>
    static void Test_NoCandidates_AutoResolved_CanAdvance()
    {
        var gc = new GameController(
            new GameState(),
            new CustomerAppearanceConfig { RareBowlNumbers = Array.Empty<int>() },
            new Random(7004),
            shop: NoShop());
        gc.StartNewGame();

        FinishPotResolveReward(gc);

        Assert(!gc.IsAwaitingCompanionChoice, "无候选时不应处于等待态");
        Assert(gc.CompanionCandidates.Count == 0, "无候选时候选列表应为空");
        Assert(gc.CanAdvanceToNextPot, "无候选时应自动 resolved，可推进");
    }

    /// <summary>8. ChooseCompanion 发布 CompanionAddedEvent，负载为新伙伴实例。</summary>
    static void Test_ChooseCompanion_PublishesCompanionAddedEvent()
    {
        var reward = CompanionData.GenerousGuestCompanion;
        var gc = SetupPotWithSatisfiedRare(reward, out _, seed: 7005);

        CompanionAddedEvent? received = null;
        gc.Events.Subscribe<CompanionAddedEvent>(e => received = e);

        gc.ChooseCompanion(reward.Id);

        Assert(received != null, "ChooseCompanion 应发布 CompanionAddedEvent");
        Assert(ReferenceEquals(received!.Companion, gc.Player.Companions[0]),
            "事件负载应为新加入的伙伴实例");
    }

    /// <summary>同一伙伴候选去重：两碗满意的同一稀有食客只产生 1 个候选。</summary>
    static void Test_DuplicateRewards_AreDeduplicated()
    {
        var reward = CompanionData.GenerousGuestCompanion;
        var state = new GameState();
        FillBasketWithRice(state, 3);
        var config = ConfigWithRare(new[] { 1, 2 }, MakeRareWithCompanion(reward));
        var gc = new GameController(state, config, new Random(7006));
        gc.StartNewGame();

        gc.SelectIngredient(gc.CurrentCandidates[0].InstanceId); // 第 1 碗满意
        Assert(gc.Pot.BowlNumber == 2, "应已进入第 2 碗");
        gc.SelectIngredient(gc.CurrentCandidates[0].InstanceId); // 第 2 碗满意
        Assert(gc.State.Customer.SatisfiedRareCustomers.Count == 2,
            "两碗应各记录 1 个满意的稀有食客");

        FinishPotResolveReward(gc);

        Assert(gc.CompanionCandidates.Count == 1,
            $"同一伙伴奖励应去重为 1 个候选，实际 {gc.CompanionCandidates.Count}");
    }

    /// <summary>非法调用：非等待态 ChooseCompanion 抛；等待态传不存在 id 抛且不消耗候选态。</summary>
    static void Test_ChooseCompanion_Invalid_Throws()
    {
        var reward = CompanionData.GenerousGuestCompanion;
        var gc = SetupPotWithSatisfiedRare(reward, out _, seed: 7007);

        bool badIdThrew = false;
        try { gc.ChooseCompanion("not_a_candidate"); }
        catch (ArgumentException) { badIdThrew = true; }
        Assert(badIdThrew, "传不存在的 id 应抛 ArgumentException");
        Assert(gc.IsAwaitingCompanionChoice, "非法选择不应消耗候选态");

        gc.ChooseCompanion(reward.Id);

        bool notAwaitingThrew = false;
        try { gc.ChooseCompanion(reward.Id); }
        catch (InvalidOperationException) { notAwaitingThrew = true; }
        Assert(notAwaitingThrew, "非等待态 ChooseCompanion 应抛 InvalidOperationException");
    }

    /// <summary>非法调用：非等待态 SkipCompanionChoice 抛。</summary>
    static void Test_SkipCompanionChoice_Invalid_Throws()
    {
        var reward = CompanionData.GenerousGuestCompanion;
        var gc = SetupPotWithSatisfiedRare(reward, out _, seed: 7008);

        gc.SkipCompanionChoice();

        bool threw = false;
        try { gc.SkipCompanionChoice(); }
        catch (InvalidOperationException) { threw = true; }
        Assert(threw, "非等待态 SkipCompanionChoice 应抛 InvalidOperationException");
    }

    /// <summary>
    /// 预览体现 E1：装备「豪爽客」后，PreviewIngredient 的 PreviewBaseScore 应比无伙伴时 +1，
    /// 且预览不修改真实状态。固定种子保证候选可复现。
    /// </summary>
    static void Test_PreviewIngredient_WithGenerousGuest_AddsOne()
    {
        var state = new GameState();
        var gc = new GameController(
            state,
            new CustomerAppearanceConfig { RareBowlNumbers = Array.Empty<int>() },
            new Random(7009));
        gc.StartNewGame();

        var candidate = gc.CurrentCandidates[0];
        int baseline = gc.PreviewIngredient(candidate).PreviewBaseScore;

        // CompanionSystem 复用 PlayerState.Companions 的同一列表，直接加入即视为装备。
        state.Player.Companions.Add(new CompanionInstance(CompanionData.GenerousGuestCompanion));

        int withCompanion = gc.PreviewIngredient(candidate).PreviewBaseScore;

        Assert(withCompanion == baseline + 1,
            $"装备豪爽客后预览基础分应 +1（{baseline} → {baseline + 1}），实际 {withCompanion}");
    }

    static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception($"[FAIL] CompanionFlowTests: {message}");
    }
}
