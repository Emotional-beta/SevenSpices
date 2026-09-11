using SevenSpices.Core.Items;

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

        Console.WriteLine("All ItemDefinitionTests passed.");
    }

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

    static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception($"[FAIL] {message}");
    }
}
