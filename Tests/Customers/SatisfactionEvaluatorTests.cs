using SevenSpices.Core.Customers;
using SevenSpices.Core.Game;

namespace SevenSpices.Tests.Customers;

public static class SatisfactionEvaluatorTests
{
    public static void RunAll()
    {
        Test_ScoreAtLeast_Satisfied();
        Test_ScoreAtLeast_NotSatisfied();
        Test_ScoreAtLeast_ExactThreshold();
        Test_FlavorAtLeast_Satisfied();
        Test_FlavorAtLeast_NotSatisfied();
        Test_FlavorAtLeast_ZeroFlavor();
        Test_MultipleConditions_OR_FirstSatisfied();
        Test_MultipleConditions_OR_NoneSatisfied();
        Test_NoConditions_ReturnsFalse();
        Test_FlavorAtLeast_NullFlavorTarget_ReturnsFalse();

        Console.WriteLine("All SatisfactionEvaluatorTests passed.");
    }

    static void Test_ScoreAtLeast_Satisfied()
    {
        var def = MakeCustomer(new CustomerSatisfactionCondition(ConditionType.ScoreAtLeast, 20));
        var pot = MakePot(finalScore: 30);
        Assert(SatisfactionEvaluator.IsSatisfied(def, pot), "FinalScore=30 >= 20 must be satisfied");
    }

    static void Test_ScoreAtLeast_NotSatisfied()
    {
        var def = MakeCustomer(new CustomerSatisfactionCondition(ConditionType.ScoreAtLeast, 20));
        var pot = MakePot(finalScore: 10);
        Assert(!SatisfactionEvaluator.IsSatisfied(def, pot), "FinalScore=10 < 20 must not be satisfied");
    }

    static void Test_ScoreAtLeast_ExactThreshold()
    {
        var def = MakeCustomer(new CustomerSatisfactionCondition(ConditionType.ScoreAtLeast, 20));
        var pot = MakePot(finalScore: 20);
        Assert(SatisfactionEvaluator.IsSatisfied(def, pot), "FinalScore=20 == 20 must be satisfied (boundary)");
    }

    static void Test_FlavorAtLeast_Satisfied()
    {
        var def = MakeCustomer(new CustomerSatisfactionCondition(ConditionType.FlavorAtLeast, 10, FlavorType.Sweet));
        var pot = MakePot(flavor: FlavorType.Sweet, flavorValue: 15);
        Assert(SatisfactionEvaluator.IsSatisfied(def, pot), "Sweet=15 >= 10 must be satisfied");
    }

    static void Test_FlavorAtLeast_NotSatisfied()
    {
        var def = MakeCustomer(new CustomerSatisfactionCondition(ConditionType.FlavorAtLeast, 10, FlavorType.Sweet));
        var pot = MakePot(flavor: FlavorType.Sweet, flavorValue: 5);
        Assert(!SatisfactionEvaluator.IsSatisfied(def, pot), "Sweet=5 < 10 must not be satisfied");
    }

    static void Test_FlavorAtLeast_ZeroFlavor()
    {
        var def = MakeCustomer(new CustomerSatisfactionCondition(ConditionType.FlavorAtLeast, 1, FlavorType.Sweet));
        var pot = new PotState(); // Sweet not set, defaults to 0
        Assert(!SatisfactionEvaluator.IsSatisfied(def, pot), "Sweet not present (0) < 1 must not be satisfied");
    }

    static void Test_MultipleConditions_OR_FirstSatisfied()
    {
        var def = MakeCustomer(
            new CustomerSatisfactionCondition(ConditionType.ScoreAtLeast, 100),
            new CustomerSatisfactionCondition(ConditionType.FlavorAtLeast, 5, FlavorType.Sweet));
        var pot = MakePot(finalScore: 5, flavor: FlavorType.Sweet, flavorValue: 10);
        Assert(SatisfactionEvaluator.IsSatisfied(def, pot),
            "Second condition (Sweet=10>=5) satisfied, OR result must be true");
    }

    static void Test_MultipleConditions_OR_NoneSatisfied()
    {
        var def = MakeCustomer(
            new CustomerSatisfactionCondition(ConditionType.ScoreAtLeast, 100),
            new CustomerSatisfactionCondition(ConditionType.FlavorAtLeast, 20, FlavorType.Sweet));
        var pot = MakePot(finalScore: 5, flavor: FlavorType.Sweet, flavorValue: 3);
        Assert(!SatisfactionEvaluator.IsSatisfied(def, pot),
            "Neither condition satisfied, OR result must be false");
    }

    static void Test_NoConditions_ReturnsFalse()
    {
        var def = new CustomerDefinition("empty_001", "无条件食客");
        var pot = MakePot(finalScore: 999);
        Assert(!SatisfactionEvaluator.IsSatisfied(def, pot),
            "No conditions must return false");
    }

    static void Test_FlavorAtLeast_NullFlavorTarget_ReturnsFalse()
    {
        var cond = new CustomerSatisfactionCondition(ConditionType.FlavorAtLeast, 5);
        var def = MakeCustomer(cond);
        var pot = MakePot(finalScore: 0);
        Assert(!SatisfactionEvaluator.IsSatisfied(def, pot),
            "FlavorAtLeast with null FlavorTarget must return false without exception");
    }

    static CustomerDefinition MakeCustomer(params CustomerSatisfactionCondition[] conditions) =>
        new("test_customer", "测试食客", isRare: true, satisfactionConditions: conditions);

    static PotState MakePot(int finalScore = 0, FlavorType? flavor = null, int flavorValue = 0)
    {
        var pot = new PotState { FinalScore = finalScore };
        if (flavor.HasValue)
            pot.AddFlavor(flavor.Value, flavorValue);
        return pot;
    }

    static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception($"[FAIL] {message}");
    }
}
