using SevenSpices.Core.Customers;
using SevenSpices.Core.Game;

namespace SevenSpices.Tests.Customers;

/// <summary>
/// CustomerDefinition 静态数据结构基本测试。
/// </summary>
public static class CustomerDefinitionTests
{
    public static void RunAll()
    {
        Test_Create_StoresIdAndName();
        Test_Create_StoresConditions();
        Test_Condition_StoresConditionTypeAndThreshold();
        Test_Condition_FlavorAtLeast_StoresFlavorTarget();
        Test_Create_NoConditions_ReturnsEmptyList();
        Test_Create_EmptyId_Throws();
        Test_Create_EmptyName_Throws();
        Test_Condition_NegativeThreshold_Throws();
        Test_IsRare_DefaultIsFalse();
        Test_IsRare_True();

        Console.WriteLine("All CustomerDefinitionTests passed.");
    }

    static void Test_Create_StoresIdAndName()
    {
        var def = new CustomerDefinition("customer_001", "老饕");
        Assert(def.Id == "customer_001", "Id must be stored correctly");
        Assert(def.Name == "老饕", "Name must be stored correctly");
    }

    static void Test_Create_StoresConditions()
    {
        var conditions = new[]
        {
            new CustomerSatisfactionCondition(ConditionType.ScoreAtLeast, 20),
        };
        var def = new CustomerDefinition("customer_002", "挑剔客", isRare: true, satisfactionConditions: conditions);
        Assert(def.SatisfactionConditions.Count == 1, "SatisfactionConditions count must match");
        Assert(def.SatisfactionConditions[0].ConditionType == ConditionType.ScoreAtLeast,
            "ConditionType must be stored correctly");
        Assert(def.SatisfactionConditions[0].Threshold == 20, "Threshold must be stored correctly");
    }

    static void Test_Condition_StoresConditionTypeAndThreshold()
    {
        var cond = new CustomerSatisfactionCondition(ConditionType.ScoreAtLeast, 50);
        Assert(cond.ConditionType == ConditionType.ScoreAtLeast, "ConditionType must be ScoreAtLeast");
        Assert(cond.Threshold == 50, "Threshold must be 50");
        Assert(cond.FlavorTarget == null, "FlavorTarget must be null when not specified");
    }

    static void Test_Condition_FlavorAtLeast_StoresFlavorTarget()
    {
        var cond = new CustomerSatisfactionCondition(ConditionType.FlavorAtLeast, 10, FlavorType.Sweet);
        Assert(cond.ConditionType == ConditionType.FlavorAtLeast, "ConditionType must be FlavorAtLeast");
        Assert(cond.Threshold == 10, "Threshold must be 10");
        Assert(cond.FlavorTarget == FlavorType.Sweet, "FlavorTarget must be FlavorType.Sweet");
    }

    static void Test_Create_NoConditions_ReturnsEmptyList()
    {
        var def = new CustomerDefinition("customer_003", "随意客");
        Assert(def.SatisfactionConditions.Count == 0, "No conditions should return empty list");
    }

    static void Test_Create_EmptyId_Throws()
    {
        try
        {
            _ = new CustomerDefinition("", "测试");
            Assert(false, "Empty Id must throw ArgumentException");
        }
        catch (ArgumentException) { }
    }

    static void Test_Create_EmptyName_Throws()
    {
        try
        {
            _ = new CustomerDefinition("id_001", "");
            Assert(false, "Empty Name must throw ArgumentException");
        }
        catch (ArgumentException) { }
    }

    static void Test_Condition_NegativeThreshold_Throws()
    {
        try
        {
            _ = new CustomerSatisfactionCondition(ConditionType.ScoreAtLeast, -1);
            Assert(false, "Negative Threshold must throw ArgumentOutOfRangeException");
        }
        catch (ArgumentOutOfRangeException) { }
    }

    static void Test_IsRare_DefaultIsFalse()
    {
        var def = new CustomerDefinition("customer_normal", "普通食客");
        Assert(!def.IsRare, "Default IsRare must be false");
    }

    static void Test_IsRare_True()
    {
        var def = new CustomerDefinition("customer_rare", "稀有食客", isRare: true);
        Assert(def.IsRare, "IsRare must be true when explicitly set");
    }

    static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception($"[FAIL] {message}");
    }
}
