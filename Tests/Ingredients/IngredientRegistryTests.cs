using SevenSpices.Core.Ingredients;

namespace SevenSpices.Tests.Ingredients;

public static class IngredientRegistryTests
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

        Console.WriteLine("All IngredientRegistryTests passed.");
    }

    static IngredientDefinition MakeDef(string id, string name) =>
        new(id, name, IngredientRarity.Common, 5);

    static void Test_Get_ReturnsCorrectDefinition()
    {
        var def = MakeDef("rice", "米饭");
        var registry = new IngredientRegistry(new[] { def });

        var result = registry.Get("rice");
        Assert(ReferenceEquals(result, def), "Get must return the same object reference that was registered");
        Assert(result.Id == "rice", "Get must return definition with correct Id");
        Assert(result.Name == "米饭", "Get must return definition with correct Name");
    }

    static void Test_Get_UnknownId_Throws()
    {
        var registry = new IngredientRegistry(new[] { MakeDef("rice", "米饭") });

        try
        {
            _ = registry.Get("nonexistent");
            Assert(false, "Get with unknown Id must throw ArgumentException");
        }
        catch (ArgumentException) { }
    }

    static void Test_GetAll_ReturnsAllDefinitions()
    {
        var a = MakeDef("rice", "米饭");
        var b = MakeDef("tofu", "豆腐");
        var c = MakeDef("egg", "鸡蛋");
        var registry = new IngredientRegistry(new[] { a, b, c });

        var all = registry.GetAll();
        Assert(all.Count == 3, "GetAll must return all 3 definitions");
        Assert(all.Any(d => d.Id == "rice"), "GetAll must include rice");
        Assert(all.Any(d => d.Id == "tofu"), "GetAll must include tofu");
        Assert(all.Any(d => d.Id == "egg"), "GetAll must include egg");
    }

    static void Test_DuplicateId_Throws()
    {
        var a = MakeDef("rice", "米饭");
        var b = MakeDef("rice", "白米饭");

        try
        {
            _ = new IngredientRegistry(new[] { a, b });
            Assert(false, "Duplicate Id must throw ArgumentException");
        }
        catch (ArgumentException) { }
    }

    static void Test_GetAll_IsReadOnly()
    {
        var registry = new IngredientRegistry(new[] { MakeDef("rice", "米饭") });
        var all = registry.GetAll();

        try
        {
            ((System.Collections.Generic.IList<IngredientDefinition>)all).Add(MakeDef("tofu", "豆腐"));
            Assert(false, "GetAll result must be read-only");
        }
        catch (NotSupportedException) { }

        Assert(registry.GetAll().Count == 1, "Internal collection must not be modified via GetAll result");
    }

    static void Test_EmptyRegistry_GetAll_ReturnsEmpty()
    {
        var registry = new IngredientRegistry(Array.Empty<IngredientDefinition>());
        Assert(registry.GetAll().Count == 0, "Empty registry GetAll must return empty list");
    }

    static void Test_EmptyRegistry_Get_Throws()
    {
        var registry = new IngredientRegistry(Array.Empty<IngredientDefinition>());

        try
        {
            _ = registry.Get("rice");
            Assert(false, "Get on empty registry must throw ArgumentException");
        }
        catch (ArgumentException) { }
    }

    static void Test_Get_EmptyId_Throws()
    {
        var registry = new IngredientRegistry(new[] { MakeDef("rice", "米饭") });

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
