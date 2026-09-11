using SevenSpices.Core.Customers;
using SevenSpices.Core.Game;
using SevenSpices.Core.Ingredients;
using SevenSpices.Core.Items;

namespace SevenSpices.Tests.Customers;

public static class CustomerServiceTests
{
    public static void RunAll()
    {
        Test_AssignCustomer_StoresInstanceReference();
        Test_AssignCustomer_AddsToAppearedCustomers();
        Test_NormalCustomer_GrantsGold();
        Test_NormalCustomer_DoesNotEnterSatisfiedRareCustomers();
        Test_NormalCustomer_ClearsCurrentCustomer();
        Test_NormalCustomer_NoRewardIngredientOrItem();
        Test_RareCustomer_Satisfied_EntersRareList();
        Test_RareCustomer_Satisfied_SameInstanceReference();
        Test_RareCustomer_Satisfied_RewardIngredientAdded();
        Test_RareCustomer_Satisfied_RewardItemAdded();
        Test_RareCustomer_Satisfied_ClearsCurrentCustomer();
        Test_RareCustomer_NotSatisfied_DoesNotEnterRareList();
        Test_RareCustomer_NotSatisfied_NoRewardAdded();
        Test_RareCustomer_NotSatisfied_ClearsCurrentCustomer();
        Test_NormalCustomer_DoesNotRunSatisfactionEvaluator();
        Test_RewardIngredientIsNull_NoIngredientAdded();
        Test_RewardItemIsNull_NoItemAdded();
        Test_EvaluateAndReward_NoCurrentCustomer_Throws();

        Console.WriteLine("All CustomerServiceTests passed.");
    }

    // ── 辅助 ──────────────────────────────────────────────────────────────────

    static CustomerInstance MakeNormal(string id = "c_normal") =>
        new(new CustomerDefinition(id, "普通食客", isRare: false));

    static CustomerInstance MakeRare(string id = "c_rare", int scoreThreshold = 10) =>
        new(new CustomerDefinition(id, "稀有食客", isRare: true, satisfactionConditions: new[]
        {
            new CustomerSatisfactionCondition(ConditionType.ScoreAtLeast, scoreThreshold)
        }));

    static PotState MakePotWithFinalScore(int finalScore)
    {
        var pot = new PotState();
        pot.FinalScore = finalScore;
        return pot;
    }

    // ── AssignCustomer ────────────────────────────────────────────────────────

    static void Test_AssignCustomer_StoresInstanceReference()
    {
        var state = new CustomerState();
        var inst = MakeNormal();

        CustomerService.AssignCustomer(state, inst);

        Assert(ReferenceEquals(state.CurrentCustomer, inst),
            "CurrentCustomer must be the same object reference");
    }

    static void Test_AssignCustomer_AddsToAppearedCustomers()
    {
        var state = new CustomerState();
        var inst = MakeNormal();

        CustomerService.AssignCustomer(state, inst);

        Assert(state.AppearedCustomers.Count == 1, "AppearedCustomers must have 1 entry");
        Assert(ReferenceEquals(state.AppearedCustomers[0], inst),
            "AppearedCustomers[0] must be the same object reference");
    }

    // ── 普通食客 ──────────────────────────────────────────────────────────────

    static void Test_NormalCustomer_GrantsGold()
    {
        var cs = new CustomerState();
        var player = new PlayerState { Gold = 5 };
        var pot = MakePotWithFinalScore(0);
        CustomerService.AssignCustomer(cs, MakeNormal());

        CustomerService.EvaluateAndReward(cs, pot, player, baseGoldReward: 10);

        Assert(player.Gold == 15, "Normal customer must grant baseGoldReward gold");
    }

    static void Test_NormalCustomer_DoesNotEnterSatisfiedRareCustomers()
    {
        var cs = new CustomerState();
        var player = new PlayerState();
        var pot = MakePotWithFinalScore(999);
        CustomerService.AssignCustomer(cs, MakeNormal());

        CustomerService.EvaluateAndReward(cs, pot, player, baseGoldReward: 10);

        Assert(cs.SatisfiedRareCustomers.Count == 0,
            "Normal customer must not enter SatisfiedRareCustomers");
    }

    static void Test_NormalCustomer_ClearsCurrentCustomer()
    {
        var cs = new CustomerState();
        var player = new PlayerState();
        var pot = MakePotWithFinalScore(0);
        CustomerService.AssignCustomer(cs, MakeNormal());

        CustomerService.EvaluateAndReward(cs, pot, player, baseGoldReward: 0);

        Assert(cs.CurrentCustomer == null, "CurrentCustomer must be null after EvaluateAndReward");
    }

    static void Test_NormalCustomer_NoRewardIngredientOrItem()
    {
        var cs = new CustomerState();
        var player = new PlayerState();
        var pot = MakePotWithFinalScore(0);
        var rewardIngr = new IngredientInstance(
            new IngredientDefinition("rice", "米饭", IngredientRarity.Common, 5));
        var rewardItem = new ItemInstance(new ItemDefinition("sauce", "酱料"));
        CustomerService.AssignCustomer(cs, MakeNormal());

        CustomerService.EvaluateAndReward(cs, pot, player,
            baseGoldReward: 0,
            rewardIngredient: rewardIngr,
            rewardItem: rewardItem);

        Assert(player.IngredientBasket.Count == 0,
            "Normal customer must not receive rewardIngredient");
        Assert(player.Items.Count == 0,
            "Normal customer must not receive rewardItem");
    }

    // ── 稀有食客满意 ──────────────────────────────────────────────────────────

    static void Test_RareCustomer_Satisfied_EntersRareList()
    {
        var cs = new CustomerState();
        var player = new PlayerState();
        var pot = MakePotWithFinalScore(20); // threshold = 10 → satisfied
        var rare = MakeRare(scoreThreshold: 10);
        CustomerService.AssignCustomer(cs, rare);

        CustomerService.EvaluateAndReward(cs, pot, player);

        Assert(cs.SatisfiedRareCustomers.Count == 1,
            "Satisfied rare customer must be in SatisfiedRareCustomers");
    }

    static void Test_RareCustomer_Satisfied_SameInstanceReference()
    {
        var cs = new CustomerState();
        var player = new PlayerState();
        var pot = MakePotWithFinalScore(20);
        var rare = MakeRare(scoreThreshold: 10);
        CustomerService.AssignCustomer(cs, rare);

        CustomerService.EvaluateAndReward(cs, pot, player);

        Assert(ReferenceEquals(cs.SatisfiedRareCustomers[0], rare),
            "SatisfiedRareCustomers[0] must be the same CustomerInstance reference");
    }

    static void Test_RareCustomer_Satisfied_RewardIngredientAdded()
    {
        var cs = new CustomerState();
        var player = new PlayerState();
        var pot = MakePotWithFinalScore(20);
        var rare = MakeRare(scoreThreshold: 10);
        var rewardIngr = new IngredientInstance(
            new IngredientDefinition("chili", "辣椒", IngredientRarity.Uncommon, 8));
        CustomerService.AssignCustomer(cs, rare);

        CustomerService.EvaluateAndReward(cs, pot, player, rewardIngredient: rewardIngr);

        Assert(player.IngredientBasket.Count == 1, "IngredientBasket must have 1 item after reward");
        Assert(ReferenceEquals(player.IngredientBasket[0], rewardIngr),
            "Stored ingredient must be the same reference as rewardIngredient");
    }

    static void Test_RareCustomer_Satisfied_RewardItemAdded()
    {
        var cs = new CustomerState();
        var player = new PlayerState();
        var pot = MakePotWithFinalScore(20);
        var rare = MakeRare(scoreThreshold: 10);
        var rewardItem = new ItemInstance(new ItemDefinition("vinegar", "醋"));
        CustomerService.AssignCustomer(cs, rare);

        CustomerService.EvaluateAndReward(cs, pot, player, rewardItem: rewardItem);

        Assert(player.Items.Count == 1, "Items must have 1 item after reward");
        Assert(ReferenceEquals(player.Items[0], rewardItem),
            "Stored item must be the same reference as rewardItem");
    }

    static void Test_RareCustomer_Satisfied_ClearsCurrentCustomer()
    {
        var cs = new CustomerState();
        var player = new PlayerState();
        var pot = MakePotWithFinalScore(20);
        CustomerService.AssignCustomer(cs, MakeRare(scoreThreshold: 10));

        CustomerService.EvaluateAndReward(cs, pot, player);

        Assert(cs.CurrentCustomer == null,
            "CurrentCustomer must be null after satisfied rare customer evaluation");
    }

    // ── 稀有食客不满意 ────────────────────────────────────────────────────────

    static void Test_RareCustomer_NotSatisfied_DoesNotEnterRareList()
    {
        var cs = new CustomerState();
        var player = new PlayerState();
        var pot = MakePotWithFinalScore(5); // threshold = 10 → not satisfied
        CustomerService.AssignCustomer(cs, MakeRare(scoreThreshold: 10));

        CustomerService.EvaluateAndReward(cs, pot, player);

        Assert(cs.SatisfiedRareCustomers.Count == 0,
            "Unsatisfied rare customer must not enter SatisfiedRareCustomers");
    }

    static void Test_RareCustomer_NotSatisfied_NoRewardAdded()
    {
        var cs = new CustomerState();
        var player = new PlayerState();
        var pot = MakePotWithFinalScore(5);
        var rewardIngr = new IngredientInstance(
            new IngredientDefinition("rice", "米饭", IngredientRarity.Common, 5));
        var rewardItem = new ItemInstance(new ItemDefinition("sauce", "酱料"));
        CustomerService.AssignCustomer(cs, MakeRare(scoreThreshold: 10));

        CustomerService.EvaluateAndReward(cs, pot, player,
            rewardIngredient: rewardIngr, rewardItem: rewardItem);

        Assert(player.IngredientBasket.Count == 0,
            "Unsatisfied rare customer must not receive rewardIngredient");
        Assert(player.Items.Count == 0,
            "Unsatisfied rare customer must not receive rewardItem");
    }

    static void Test_RareCustomer_NotSatisfied_ClearsCurrentCustomer()
    {
        var cs = new CustomerState();
        var player = new PlayerState();
        var pot = MakePotWithFinalScore(5);
        CustomerService.AssignCustomer(cs, MakeRare(scoreThreshold: 10));

        CustomerService.EvaluateAndReward(cs, pot, player);

        Assert(cs.CurrentCustomer == null,
            "CurrentCustomer must be null after unsatisfied rare customer evaluation");
    }

    // ── 普通食客不触发稀有判断 ────────────────────────────────────────────────

    static void Test_NormalCustomer_DoesNotRunSatisfactionEvaluator()
    {
        // 构造一个普通食客，但 FinalScore 很高——如果错误调用了 SatisfactionEvaluator
        // 并且判断了满意，会把它加入 SatisfiedRareCustomers（可以检测到）
        var cs = new CustomerState();
        var player = new PlayerState();
        var normalWithCondition = new CustomerInstance(
            new CustomerDefinition("trick", "伪装普通食客", isRare: false,
                satisfactionConditions: new[] {
                    new CustomerSatisfactionCondition(ConditionType.ScoreAtLeast, 0)
                }));
        var pot = MakePotWithFinalScore(9999);
        CustomerService.AssignCustomer(cs, normalWithCondition);

        CustomerService.EvaluateAndReward(cs, pot, player, baseGoldReward: 5);

        Assert(cs.SatisfiedRareCustomers.Count == 0,
            "Normal customer (IsRare=false) must never enter SatisfiedRareCustomers, " +
            "even if satisfaction conditions would be met");
    }

    // ── 边界 ──────────────────────────────────────────────────────────────────

    static void Test_RewardIngredientIsNull_NoIngredientAdded()
    {
        var cs = new CustomerState();
        var player = new PlayerState();
        var pot = MakePotWithFinalScore(20);
        CustomerService.AssignCustomer(cs, MakeRare(scoreThreshold: 10));

        CustomerService.EvaluateAndReward(cs, pot, player, rewardIngredient: null);

        Assert(player.IngredientBasket.Count == 0,
            "null rewardIngredient must not add anything to IngredientBasket");
    }

    static void Test_RewardItemIsNull_NoItemAdded()
    {
        var cs = new CustomerState();
        var player = new PlayerState();
        var pot = MakePotWithFinalScore(20);
        CustomerService.AssignCustomer(cs, MakeRare(scoreThreshold: 10));

        CustomerService.EvaluateAndReward(cs, pot, player, rewardItem: null);

        Assert(player.Items.Count == 0,
            "null rewardItem must not add anything to Items");
    }

    static void Test_EvaluateAndReward_NoCurrentCustomer_Throws()
    {
        var cs = new CustomerState(); // CurrentCustomer == null
        var player = new PlayerState();
        var pot = new PotState();

        try
        {
            CustomerService.EvaluateAndReward(cs, pot, player);
            Assert(false, "EvaluateAndReward with no CurrentCustomer must throw InvalidOperationException");
        }
        catch (InvalidOperationException) { }
    }

    // ─────────────────────────────────────────────────────────────────────────

    static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception($"[FAIL] CustomerServiceTests: {message}");
    }
}
