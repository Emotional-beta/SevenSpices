using SevenSpices.Core.Effects;
using SevenSpices.Core.Items;
using SevenSpices.Tests.Effects;

namespace SevenSpices.Tests.Items;

public static class ItemDefinitionTests
{
    public static void RunAll()
    {
        Test_Create_StoresIdAndName();
        Test_Create_EmptyId_Throws();
        Test_Create_EmptyName_Throws();
        Test_Create_WhitespaceId_Throws();
        Test_Create_WhitespaceName_Throws();
        Test_NoEffects_ReturnsEmptyList();
        Test_NullEffects_ReturnsEmptyList();
        Test_Effects_CountCorrect();
        Test_Effects_OrderPreserved();
        Test_Effects_SameReferenceStored();
        Test_Effects_IsReadOnly();

        Console.WriteLine("All ItemDefinitionTests passed.");
    }

    static IEffect MakeEffect(string id) =>
        new LambdaEffect(id, _ => { });

    static void Test_Create_StoresIdAndName()
    {
        var def = new ItemDefinition("item_001", "辣椒酱");
        Assert(def.Id == "item_001", "Id must be stored correctly");
        Assert(def.Name == "辣椒酱", "Name must be stored correctly");
    }

    static void Test_Create_EmptyId_Throws()
    {
        try
        {
            _ = new ItemDefinition("", "测试道具");
            Assert(false, "Empty Id must throw ArgumentException");
        }
        catch (ArgumentException) { }
    }

    static void Test_Create_EmptyName_Throws()
    {
        try
        {
            _ = new ItemDefinition("item_001", "");
            Assert(false, "Empty Name must throw ArgumentException");
        }
        catch (ArgumentException) { }
    }

    static void Test_Create_WhitespaceId_Throws()
    {
        try
        {
            _ = new ItemDefinition("   ", "测试道具");
            Assert(false, "Whitespace Id must throw ArgumentException");
        }
        catch (ArgumentException) { }
    }

    static void Test_Create_WhitespaceName_Throws()
    {
        try
        {
            _ = new ItemDefinition("item_001", "   ");
            Assert(false, "Whitespace Name must throw ArgumentException");
        }
        catch (ArgumentException) { }
    }

    static void Test_NoEffects_ReturnsEmptyList()
    {
        var def = new ItemDefinition("item_001", "辣椒酱");
        Assert(def.Effects.Count == 0, "Effects must be empty when not provided");
    }

    static void Test_NullEffects_ReturnsEmptyList()
    {
        var def = new ItemDefinition("item_001", "辣椒酱", null);
        Assert(def.Effects.Count == 0, "Effects must be empty when null is passed");
    }

    static void Test_Effects_CountCorrect()
    {
        var effects = new[] { MakeEffect("e1"), MakeEffect("e2") };
        var def = new ItemDefinition("item_001", "辣椒酱", effects);
        Assert(def.Effects.Count == 2, "Effects count must match the number passed in");
    }

    static void Test_Effects_OrderPreserved()
    {
        var e1 = MakeEffect("e1");
        var e2 = MakeEffect("e2");
        var def = new ItemDefinition("item_001", "辣椒酱", new[] { e1, e2 });
        Assert(def.Effects[0].EffectId == "e1", "First effect must be e1");
        Assert(def.Effects[1].EffectId == "e2", "Second effect must be e2");
    }

    static void Test_Effects_SameReferenceStored()
    {
        var e1 = MakeEffect("e1");
        var e2 = MakeEffect("e2");
        var def = new ItemDefinition("item_001", "辣椒酱", new[] { e1, e2 });
        Assert(ReferenceEquals(def.Effects[0], e1), "Effects[0] must be the same object reference");
        Assert(ReferenceEquals(def.Effects[1], e2), "Effects[1] must be the same object reference");
    }

    static void Test_Effects_IsReadOnly()
    {
        var def = new ItemDefinition("item_001", "辣椒酱", new[] { MakeEffect("e1") });

        try
        {
            ((System.Collections.Generic.IList<IEffect>)def.Effects).Add(MakeEffect("e2"));
            Assert(false, "Effects must be read-only");
        }
        catch (NotSupportedException) { }

        Assert(def.Effects.Count == 1, "Internal collection must not be modified via Effects");
    }

    static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception($"[FAIL] {message}");
    }
}

