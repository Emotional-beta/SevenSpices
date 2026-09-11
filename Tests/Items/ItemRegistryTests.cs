using SevenSpices.Core.Items;

namespace SevenSpices.Tests.Items;

public static class ItemRegistryTests
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

        Console.WriteLine("All ItemRegistryTests passed.");
    }

    static ItemDefinition MakeDef(string id, string name) =>
        new(id, name);

    static void Test_Get_ReturnsCorrectDefinition()
    {
        var def = MakeDef("item_001", "辣椒酱");
        var registry = new ItemRegistry(new[] { def });

        var result = registry.Get("item_001");
        Assert(ReferenceEquals(result, def), "Get must return the same object reference that was registered");
        Assert(result.Id == "item_001", "Get must return definition with correct Id");
        Assert(result.Name == "辣椒酱", "Get must return definition with correct Name");
    }

    static void Test_Get_UnknownId_Throws()
    {
        var registry = new ItemRegistry(new[] { MakeDef("item_001", "辣椒酱") });

        try
        {
            _ = registry.Get("nonexistent");
            Assert(false, "Get with unknown Id must throw ArgumentException");
        }
        catch (ArgumentException) { }
    }

    static void Test_GetAll_ReturnsAllDefinitions()
    {
        var a = MakeDef("item_001", "辣椒酱");
        var b = MakeDef("item_002", "醋");
        var c = MakeDef("item_003", "盐");
        var registry = new ItemRegistry(new[] { a, b, c });

        var all = registry.GetAll();
        Assert(all.Count == 3, "GetAll must return all 3 definitions");
        Assert(all.Any(d => d.Id == "item_001"), "GetAll must include item_001");
        Assert(all.Any(d => d.Id == "item_002"), "GetAll must include item_002");
        Assert(all.Any(d => d.Id == "item_003"), "GetAll must include item_003");
    }

    static void Test_DuplicateId_Throws()
    {
        var a = MakeDef("item_001", "辣椒酱");
        var b = MakeDef("item_001", "另一瓶辣椒酱");

        try
        {
            _ = new ItemRegistry(new[] { a, b });
            Assert(false, "Duplicate Id must throw ArgumentException");
        }
        catch (ArgumentException) { }
    }

    static void Test_GetAll_IsReadOnly()
    {
        var registry = new ItemRegistry(new[] { MakeDef("item_001", "辣椒酱") });
        var all = registry.GetAll();

        try
        {
            ((System.Collections.Generic.IList<ItemDefinition>)all).Add(MakeDef("item_002", "醋"));
            Assert(false, "GetAll result must be read-only");
        }
        catch (NotSupportedException) { }

        Assert(registry.GetAll().Count == 1, "Internal collection must not be modified via GetAll result");
    }

    static void Test_EmptyRegistry_GetAll_ReturnsEmpty()
    {
        var registry = new ItemRegistry(Array.Empty<ItemDefinition>());
        Assert(registry.GetAll().Count == 0, "Empty registry GetAll must return empty list");
    }

    static void Test_EmptyRegistry_Get_Throws()
    {
        var registry = new ItemRegistry(Array.Empty<ItemDefinition>());

        try
        {
            _ = registry.Get("item_001");
            Assert(false, "Get on empty registry must throw ArgumentException");
        }
        catch (ArgumentException) { }
    }

    static void Test_Get_EmptyId_Throws()
    {
        var registry = new ItemRegistry(new[] { MakeDef("item_001", "辣椒酱") });

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
