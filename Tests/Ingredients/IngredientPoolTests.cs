using SevenSpices.Core.Game;
using SevenSpices.Core.Ingredients;

namespace SevenSpices.Tests.Ingredients;

/// <summary>
/// 食材实例唯一性、食材池抽取规则测试。
/// 使用最小化的内联断言，不依赖任何测试框架（可在 dotnet script 或 NUnit/xUnit 中运行）。
/// Phase 1 先用简单 assert 验证逻辑。
/// </summary>
public static class IngredientPoolTests
{
    public static void RunAll()
    {
        Test_InstanceIds_AreUnique();
        Test_Draw_ReturnsUpToThree();
        Test_Draw_WhenFewerThanThree_ReturnsAll();
        Test_Draw_WhenEmpty_ReturnsEmpty();
        Test_Confirm_RemovesSelected_ReturnsOthers();
        Test_ReturnCandidates_AllReturnToPool();
        Test_NoDuplicates_InSingleDraw();
        Test_ExplicitInstanceId_IsPreserved();

        Console.WriteLine("All IngredientPoolTests passed.");
    }

    static IngredientDefinition MakeRice() =>
        new("rice", "米饭", IngredientRarity.Common, 5);

    static void Test_InstanceIds_AreUnique()
    {
        var def = MakeRice();
        var a = new IngredientInstance(def);
        var b = new IngredientInstance(def);
        var c = new IngredientInstance(def);

        Assert(a.InstanceId != b.InstanceId, "Instance IDs must be unique (a vs b)");
        Assert(b.InstanceId != c.InstanceId, "Instance IDs must be unique (b vs c)");
        Assert(a.InstanceId != c.InstanceId, "Instance IDs must be unique (a vs c)");
    }

    static void Test_Draw_ReturnsUpToThree()
    {
        var def = MakeRice();
        var pool = new IngredientPool(new[]
        {
            new IngredientInstance(def),
            new IngredientInstance(def),
            new IngredientInstance(def),
            new IngredientInstance(def),
            new IngredientInstance(def),
        });

        var candidates = pool.Draw(3);
        Assert(candidates.Count == 3, "Draw(3) from 5 should return 3 candidates");
        Assert(pool.Count == 2, "Pool should have 2 remaining after Draw(3) from 5");

        pool.ReturnCandidates(candidates);
    }

    static void Test_Draw_WhenFewerThanThree_ReturnsAll()
    {
        var def = MakeRice();
        var pool = new IngredientPool(new[]
        {
            new IngredientInstance(def),
            new IngredientInstance(def),
        });

        var candidates = pool.Draw(3);
        Assert(candidates.Count == 2, "Draw(3) from 2 should return 2 (all available)");
        Assert(pool.Count == 0, "Pool should be empty after drawing all");

        pool.ReturnCandidates(candidates);
    }

    static void Test_Draw_WhenEmpty_ReturnsEmpty()
    {
        var pool = new IngredientPool();
        var candidates = pool.Draw(3);
        Assert(candidates.Count == 0, "Draw from empty pool should return empty list");
    }

    static void Test_Confirm_RemovesSelected_ReturnsOthers()
    {
        var def = MakeRice();
        var pool = new IngredientPool(new[]
        {
            new IngredientInstance(def, "rice_test_001"),
            new IngredientInstance(def, "rice_test_002"),
            new IngredientInstance(def, "rice_test_003"),
        });

        var candidates = pool.Draw(3);
        Assert(pool.Count == 0, "Pool should be empty after draw");

        var selected = pool.Confirm(candidates, "rice_test_001");
        Assert(selected.InstanceId == "rice_test_001", "Confirm should return selected instance");
        Assert(pool.Count == 2, "Unselected 2 candidates should be returned to pool");

        var poolIds = pool.GetAll().Select(i => i.InstanceId).ToHashSet();
        Assert(!poolIds.Contains("rice_test_001"), "Selected instance must NOT be in pool");
        Assert(poolIds.Contains("rice_test_002"), "rice_test_002 must be back in pool");
        Assert(poolIds.Contains("rice_test_003"), "rice_test_003 must be back in pool");
    }

    static void Test_ReturnCandidates_AllReturnToPool()
    {
        var def = MakeRice();
        var pool = new IngredientPool(new[]
        {
            new IngredientInstance(def),
            new IngredientInstance(def),
            new IngredientInstance(def),
        });

        var candidates = pool.Draw(3);
        Assert(pool.Count == 0, "Pool should be empty after draw");

        pool.ReturnCandidates(candidates);
        Assert(pool.Count == 3, "All candidates should be back after ReturnCandidates");
    }

    static void Test_NoDuplicates_InSingleDraw()
    {
        var def = MakeRice();
        var pool = new IngredientPool(new[]
        {
            new IngredientInstance(def),
            new IngredientInstance(def),
            new IngredientInstance(def),
            new IngredientInstance(def),
            new IngredientInstance(def),
        });

        var candidates = pool.Draw(3);
        var ids = candidates.Select(c => c.InstanceId).ToList();
        Assert(ids.Distinct().Count() == ids.Count, "No duplicate instances in a single draw");

        pool.ReturnCandidates(candidates);
    }

    static void Test_ExplicitInstanceId_IsPreserved()
    {
        var def = MakeRice();
        var inst = new IngredientInstance(def, "rice_test_fixed");
        Assert(inst.InstanceId == "rice_test_fixed", "Explicit instanceId must be preserved");
    }

    static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception($"[FAIL] {message}");
    }
}
