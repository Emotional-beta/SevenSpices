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
        Test_Draw_UnresolvedSecondDraw_Throws();
        Test_Draw_AfterConfirm_CanDrawAgain();
        Test_Draw_EmptyPool_Repeated_DoesNotThrow();
        Test_Confirm_MismatchedBatch_Throws_PendingPreserved();
        Test_ReturnCandidates_MismatchedBatch_Throws_PendingPreserved();
        Test_Settle_DuplicateIdsWithEqualCount_Throws();

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

        // ReturnCandidates 也应解除 pending 守卫：之后可再次 Draw 且返回非空候选。
        IReadOnlyList<IngredientInstance>? second = null;
        bool threw = false;
        try { second = pool.Draw(3); }
        catch (InvalidOperationException) { threw = true; }

        Assert(!threw, "ReturnCandidates 后再 Draw 不应抛异常");
        Assert(second != null && second.Count > 0, "ReturnCandidates 后再 Draw 应返回非空候选");
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

    /// <summary>上一批候选未 Confirm / ReturnCandidates 时再次 Draw 应抛异常，不再静默丢弃。</summary>
    static void Test_Draw_UnresolvedSecondDraw_Throws()
    {
        var def = MakeRice();
        var pool = new IngredientPool(new[]
        {
            new IngredientInstance(def),
            new IngredientInstance(def),
            new IngredientInstance(def),
            new IngredientInstance(def),
            new IngredientInstance(def),
            new IngredientInstance(def),
        });

        pool.Draw(3);

        bool threw = false;
        try { pool.Draw(3); }
        catch (InvalidOperationException) { threw = true; }

        Assert(threw, "未结算候选时二次 Draw 应抛 InvalidOperationException");
    }

    /// <summary>Confirm 结算后允许再次 Draw。</summary>
    static void Test_Draw_AfterConfirm_CanDrawAgain()
    {
        var def = MakeRice();
        var pool = new IngredientPool(new[]
        {
            new IngredientInstance(def, "rice_a"),
            new IngredientInstance(def, "rice_b"),
            new IngredientInstance(def, "rice_c"),
            new IngredientInstance(def, "rice_d"),
            new IngredientInstance(def, "rice_e"),
            new IngredientInstance(def, "rice_f"),
        });

        var first = pool.Draw(3);
        pool.Confirm(first, first[0].InstanceId);

        IReadOnlyList<IngredientInstance>? second = null;
        bool threw = false;
        try { second = pool.Draw(3); }
        catch (InvalidOperationException) { threw = true; }

        Assert(!threw, "Confirm 结算后再 Draw 不应抛异常");
        Assert(second != null && second.Count == 3, "Confirm 后再 Draw 应正常返回候选");
    }

    /// <summary>池空时连续 Draw 返回空批，不应触发未结算守卫（GameController SkipBowl 路径依赖）。</summary>
    static void Test_Draw_EmptyPool_Repeated_DoesNotThrow()
    {
        var pool = new IngredientPool();

        bool threw = false;
        try
        {
            Assert(pool.Draw(3).Count == 0, "空池首次 Draw 应返回空批");
            Assert(pool.Draw(3).Count == 0, "空池再次 Draw 应返回空批");
            Assert(pool.Draw(3).Count == 0, "空池连续 Draw 均应返回空批");
        }
        catch (InvalidOperationException) { threw = true; }

        Assert(!threw, "池空连续 Draw 不应抛 InvalidOperationException");
    }

    /// <summary>
    /// Confirm 传入非最近一批候选（空列表 / 另一批）应抛 InvalidOperationException，
    /// 且此时 pending 未被清除——随后用正确批次仍能 Confirm 成功。
    /// </summary>
    static void Test_Confirm_MismatchedBatch_Throws_PendingPreserved()
    {
        var def = MakeRice();
        var pool = new IngredientPool(new[]
        {
            new IngredientInstance(def, "rice_a"),
            new IngredientInstance(def, "rice_b"),
            new IngredientInstance(def, "rice_c"),
            new IngredientInstance(def, "rice_d"),
            new IngredientInstance(def, "rice_e"),
            new IngredientInstance(def, "rice_f"),
        });

        var batch = pool.Draw(3);

        // 空列表：数量不符。
        AssertInvalidOperation(
            () => pool.Confirm(new List<IngredientInstance>(), "rice_a"),
            "Confirm 传入空列表应抛 InvalidOperationException");

        // 另一批候选：数量可相等但 InstanceId 不在 pending 中。
        var foreign = new List<IngredientInstance>
        {
            new(def, "rice_x"),
            new(def, "rice_y"),
            new(def, "rice_z"),
        };
        AssertInvalidOperation(
            () => pool.Confirm(foreign, "rice_x"),
            "Confirm 传入另一批候选应抛 InvalidOperationException");

        // pending 未被清除：用正确批次仍能 Confirm 成功。
        var selected = pool.Confirm(batch, batch[0].InstanceId);
        Assert(selected.InstanceId == batch[0].InstanceId,
            "误传后 pending 应保留，正确批次仍能 Confirm 选中");
        Assert(pool.GetAll().Count == 5,
            "Confirm 后未选中的 2 个候选应回池（池内 3 + 2 = 5）");

        // 结算后 pending 已清空，再次误用相同批次也应抛。
        AssertInvalidOperation(
            () => pool.Confirm(batch, batch[1].InstanceId),
            "结算后 pending 已清空，再次 Confirm 应抛 InvalidOperationException");
    }

    /// <summary>
    /// ReturnCandidates 传入非最近一批候选应抛 InvalidOperationException，
    /// 且 pending 未被清除——随后用正确批次仍能全部回池。
    /// </summary>
    static void Test_ReturnCandidates_MismatchedBatch_Throws_PendingPreserved()
    {
        var def = MakeRice();
        var pool = new IngredientPool(new[]
        {
            new IngredientInstance(def, "rice_a"),
            new IngredientInstance(def, "rice_b"),
            new IngredientInstance(def, "rice_c"),
        });

        var batch = pool.Draw(3);
        Assert(pool.Count == 0, "前置：Draw 后池应为空");

        AssertInvalidOperation(
            () => pool.ReturnCandidates(new List<IngredientInstance>()),
            "ReturnCandidates 传入空列表应抛 InvalidOperationException");

        var foreign = new List<IngredientInstance> { new(def, "rice_x") };
        AssertInvalidOperation(
            () => pool.ReturnCandidates(foreign),
            "ReturnCandidates 传入另一批候选应抛 InvalidOperationException");

        // 数量相等但 ID 完全不同的另一批（对照 Confirm 侧同类用例）。
        var foreignSameCount = new List<IngredientInstance>
        {
            new(def, "rice_x"),
            new(def, "rice_y"),
            new(def, "rice_z"),
        };
        AssertInvalidOperation(
            () => pool.ReturnCandidates(foreignSameCount),
            "ReturnCandidates 传入数量相等但 ID 不同的一批应抛 InvalidOperationException");

        Assert(pool.Count == 0, "误传被拒后候选不应被放入池中");

        // pending 未被清除：用正确批次仍能全部回池。
        pool.ReturnCandidates(batch);
        Assert(pool.Count == 3, "用正确批次 ReturnCandidates 后全部候选应回池");
    }

    /// <summary>
    /// 传入批次与 pending 数量相等但含重复 ID（pending=[A,B,C] 传入 [A,A,B]）应抛异常。
    /// 仅校验「数量 + 单向包含」会放过这种批次，需两侧 InstanceId 集合完全相等。
    /// </summary>
    static void Test_Settle_DuplicateIdsWithEqualCount_Throws()
    {
        var def = MakeRice();
        var pool = new IngredientPool(new[]
        {
            new IngredientInstance(def, "rice_a"),
            new IngredientInstance(def, "rice_b"),
            new IngredientInstance(def, "rice_c"),
        });

        var batch = pool.Draw(3);
        Assert(batch.Count == 3, "前置：应从 3 个实例抽出 3 个候选");

        var dup = new List<IngredientInstance> { batch[0], batch[0], batch[1] };
        Assert(dup.Count == batch.Count, "前置：重复批次数量应与 pending 相等");

        AssertInvalidOperation(
            () => pool.Confirm(dup, batch[0].InstanceId),
            "Confirm 传入数量相等但含重复 ID 的批次应抛 InvalidOperationException");
        AssertInvalidOperation(
            () => pool.ReturnCandidates(dup),
            "ReturnCandidates 传入数量相等但含重复 ID 的批次应抛 InvalidOperationException");

        Assert(pool.Count == 0, "误传被拒后候选不应被放入池中");

        // pending 未被清除：用正确批次仍能全部回池。
        pool.ReturnCandidates(batch);
        Assert(pool.Count == 3, "误传后 pending 应保留，正确批次仍能全部回池");
    }

    static void AssertInvalidOperation(Action action, string message)
    {
        bool threw = false;
        try { action(); }
        catch (InvalidOperationException) { threw = true; }
        Assert(threw, message);
    }

    static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception($"[FAIL] {message}");
    }
}
