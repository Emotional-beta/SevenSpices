using SevenSpices.Core.Customers;
using SevenSpices.Core.Game;

namespace SevenSpices.Tests.Customers;

public static class CustomerRegistryTests
{
    public static void RunAll()
    {
        Test_Get_ReturnsCorrectDefinition();
        Test_Get_UnknownId_Throws();
        Test_GetAll_ReturnsAllDefinitions();
        Test_DuplicateId_Throws();
        Test_GetAll_IsReadOnly();
        Test_EmptyRegistry_GetAll_ReturnsEmpty();
        Test_EmptyRegistry_Get_Throws();
        Test_Get_EmptyId_Throws();

        Console.WriteLine("All CustomerRegistryTests passed.");
    }

    static CustomerDefinition MakeDef(string id, string name) =>
        new(id, name);

    static CustomerDefinition MakeRareDef(string id, string name) =>
        new(id, name, isRare: true, satisfactionConditions: new[]
        {
            new CustomerSatisfactionCondition(ConditionType.ScoreAtLeast, 20)
        });

    static void Test_Get_ReturnsCorrectDefinition()
    {
        var def = MakeDef("customer_001", "老饕");
        var registry = new CustomerRegistry(new[] { def });

        var result = registry.Get("customer_001");
        Assert(ReferenceEquals(result, def), "Get must return the same object reference that was registered");
        Assert(result.Id == "customer_001", "Get must return definition with correct Id");
        Assert(result.Name == "老饕", "Get must return definition with correct Name");
    }

    static void Test_Get_UnknownId_Throws()
    {
        var registry = new CustomerRegistry(new[] { MakeDef("customer_001", "老饕") });

        try
        {
            _ = registry.Get("nonexistent");
            Assert(false, "Get with unknown Id must throw ArgumentException");
        }
        catch (ArgumentException) { }
    }

    static void Test_GetAll_ReturnsAllDefinitions()
    {
        var a = MakeDef("customer_001", "老饕");
        var b = MakeDef("customer_002", "吃货");
        var c = MakeRareDef("customer_rare_001", "神秘食客");
        var registry = new CustomerRegistry(new[] { a, b, c });

        var all = registry.GetAll();
        Assert(all.Count == 3, "GetAll must return all 3 definitions");
        Assert(all.Any(d => d.Id == "customer_001"), "GetAll must include customer_001");
        Assert(all.Any(d => d.Id == "customer_002"), "GetAll must include customer_002");
        Assert(all.Any(d => d.Id == "customer_rare_001"), "GetAll must include customer_rare_001");
    }

    static void Test_DuplicateId_Throws()
    {
        var a = MakeDef("customer_001", "老饕");
        var b = MakeDef("customer_001", "另一个老饕");

        try
        {
            _ = new CustomerRegistry(new[] { a, b });
            Assert(false, "Duplicate Id must throw ArgumentException");
        }
        catch (ArgumentException) { }
    }

    static void Test_GetAll_IsReadOnly()
    {
        var registry = new CustomerRegistry(new[] { MakeDef("customer_001", "老饕") });
        var all = registry.GetAll();

        try
        {
            ((System.Collections.Generic.IList<CustomerDefinition>)all).Add(MakeDef("customer_002", "吃货"));
            Assert(false, "GetAll result must be read-only");
        }
        catch (NotSupportedException) { }

        Assert(registry.GetAll().Count == 1, "Internal collection must not be modified via GetAll result");
    }

    static void Test_EmptyRegistry_GetAll_ReturnsEmpty()
    {
        var registry = new CustomerRegistry(Array.Empty<CustomerDefinition>());
        Assert(registry.GetAll().Count == 0, "Empty registry GetAll must return empty list");
    }

    static void Test_EmptyRegistry_Get_Throws()
    {
        var registry = new CustomerRegistry(Array.Empty<CustomerDefinition>());

        try
        {
            _ = registry.Get("customer_001");
            Assert(false, "Get on empty registry must throw ArgumentException");
        }
        catch (ArgumentException) { }
    }

    static void Test_Get_EmptyId_Throws()
    {
        var registry = new CustomerRegistry(new[] { MakeDef("customer_001", "老饕") });

        try
        {
            _ = registry.Get("");
            Assert(false, "Get with empty Id must throw ArgumentException");
        }
        catch (ArgumentException) { }
    }

    static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception($"[FAIL] {message}");
    }
}
