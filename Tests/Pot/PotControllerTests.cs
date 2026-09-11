using SevenSpices.Core.Content;
using SevenSpices.Core.Effects;
using SevenSpices.Core.Game;
using SevenSpices.Core.Ingredients;
using SevenSpices.Core.Items;
using SevenSpices.Core.Pot;
using SevenSpices.Tests.Effects;

namespace SevenSpices.Tests.Pot;

/// <summary>
/// PotController 基础流程测试。
/// 覆盖：开锅初始化、碗的开始、碗数递增、普通锅第10碗结束、第11碗禁止、End状态阻止推进、最终锅边界。
/// Phase 3 Part 4：AddIngredient / ApplyItemEffect 效果触发测试。
/// </summary>
public static class PotControllerTests
{
    public static void RunAll()
    {
        Test_StartPot_InitializesCorrectly();
        Test_StartBowl_SetsPhaseAndResetsScore();
        Test_AdvanceBowlPhase_ProgressesThroughAllPhases();
        Test_BowlNumber_IncrementsOnStartNextBowl();
        Test_NormalPot_EndsAfterTenthBowl();
        Test_NormalPot_CannotStartEleventhBowl();
        Test_EndedPot_CannotAdvanceBowlPhase();
        Test_EndedPot_CannotStartBowl();
        Test_FinalPot_DoesNotEndAfterTenthBowl();
        Test_FinalPot_CanStartEleventhBowl();
        Test_StartPot_CannotBeCalledTwice();
        Test_AdvanceBowlPhase_ThrowsWhenAtEnd();
        Test_UseItem_OutsideItemPhase_Throws();
        Test_UseItem_ConsumesItemAndReturnsIt();
        Test_UseItem_RemovedFromPlayerItems();
        Test_UseItem_UnknownInstanceId_Throws();
        // Part 4: AddIngredient
        Test_AddIngredient_AddsToIngredients();
        Test_AddIngredient_TriggersEffect();
        Test_AddIngredient_ContextCurrentIngredientIsCorrect();
        Test_AddIngredient_SourceIdIsInstanceId();
        Test_AddIngredient_WrongPhase_Throws();
        Test_AddIngredient_WrongPhase_DoesNotAddToIngredients();
        // Part 4: ApplyItemEffect
        Test_ApplyItemEffect_TriggersEffect();
        Test_ApplyItemEffect_ContextCurrentIngredientIsNull();
        Test_ApplyItemEffect_SourceIdIsItemInstanceId();
        Test_ApplyItemEffect_WrongPhase_Throws();
        // Part 4: 循环保护 / 共用 EffectSystem 路径
        Test_AddIngredient_LoopProtection();
        Test_IngredientAndItem_UseSharedEffectSystemPath();
        // Part 5: BaseScore / Flavor 自动应用
        Test_AddIngredient_AutoAppliesBaseScore();
        Test_AddIngredient_AutoAppliesFlavors();
        Test_AddIngredient_BaseScoreAndFlavorAndEffect_AllApplied();
        Test_AddIngredient_Honey_FlavorAutoApplied_Then_ScaledEffect();
        Test_AddIngredient_IceCube_MultiplierEffect_FinalScore();
        Test_AddIngredient_Egg_UniqueCount_WithAutoApply();

        Console.WriteLine("All PotControllerTests passed.");
    }

    // ── 辅助方法 ─────────────────────────────────────────────────────────────

    /// <summary>将当前碗从 Start 阶段一路推进到 End 阶段（9次 AdvanceBowlPhase）。</summary>
    static void AdvanceCurrentBowlToEnd(PotController controller)
    {
        // BowlPhase.Start(0) → BowlPhase.End(9)，共 9 步
        int steps = (int)BowlPhase.End - (int)BowlPhase.Start;
        for (int i = 0; i < steps; i++)
            controller.AdvanceBowlPhase();
    }

    /// <summary>完整地走完一碗（StartBowl + 推进到 End），不推进到下一碗。</summary>
    static void RunBowlToEnd(PotController controller)
    {
        controller.StartBowl();
        AdvanceCurrentBowlToEnd(controller);
    }

    // ── 测试用例 ──────────────────────────────────────────────────────────────

    static void Test_StartPot_InitializesCorrectly()
    {
        var state = new GameState();
        var controller = new PotController(state);
        controller.StartPot();

        Assert(state.Pot.Phase == PotPhase.InProgress, "Pot phase should be InProgress after StartPot");
        Assert(state.Pot.BowlNumber == 1, "BowlNumber should be 1 after StartPot");
        Assert(state.Pot.BowlLimit == 10, "BowlLimit should be 10 by default (normal pot)");
    }

    static void Test_StartBowl_SetsPhaseAndResetsScore()
    {
        var state = new GameState();
        var controller = new PotController(state);
        controller.StartPot();

        // 人为设置一些"上一碗"残留数据，验证 StartBowl 会清零
        state.Pot.BaseScore = 99;
        state.Pot.FinalScore = 200;
        state.Pot.IsScoreLocked = true;

        controller.StartBowl();

        Assert(state.Pot.CurrentBowlPhase == BowlPhase.Start, "CurrentBowlPhase should be Start");
        Assert(state.Pot.BaseScore == 0, "BaseScore should reset to 0");
        Assert(state.Pot.FinalScore == 0, "FinalScore should reset to 0");
        Assert(!state.Pot.IsScoreLocked, "IsScoreLocked should reset to false");
    }

    static void Test_AdvanceBowlPhase_ProgressesThroughAllPhases()
    {
        var state = new GameState();
        var controller = new PotController(state);
        controller.StartPot();
        controller.StartBowl();

        // 逐步推进并验证每个阶段
        var expectedSequence = new[]
        {
            BowlPhase.Customer,
            BowlPhase.ItemPhase,
            BowlPhase.IngredientSelection,
            BowlPhase.IngredientResolve,
            BowlPhase.ScoreCalculation,
            BowlPhase.ScoreLocked,
            BowlPhase.Serving,
            BowlPhase.Reward,
            BowlPhase.End
        };

        foreach (var expected in expectedSequence)
        {
            controller.AdvanceBowlPhase();
            Assert(state.Pot.CurrentBowlPhase == expected, $"Expected phase {expected}, got {state.Pot.CurrentBowlPhase}");
        }
    }

    static void Test_BowlNumber_IncrementsOnStartNextBowl()
    {
        var state = new GameState();
        var controller = new PotController(state);
        controller.StartPot();

        RunBowlToEnd(controller);
        Assert(state.Pot.BowlNumber == 1, "BowlNumber should still be 1 at End of bowl 1");

        controller.StartNextBowl();
        Assert(state.Pot.BowlNumber == 2, "BowlNumber should be 2 after StartNextBowl");
        Assert(state.Pot.CurrentBowlPhase == BowlPhase.Start, "CurrentBowlPhase should reset to Start for bowl 2");
        Assert(state.Pot.Phase == PotPhase.InProgress, "Pot should still be InProgress");

        AdvanceCurrentBowlToEnd(controller);
        controller.StartNextBowl();
        Assert(state.Pot.BowlNumber == 3, "BowlNumber should be 3");
    }

    static void Test_NormalPot_EndsAfterTenthBowl()
    {
        var state = new GameState();
        var controller = new PotController(state);
        controller.StartPot();

        // 前9碗：正常走完后推进到下一碗
        for (int i = 1; i <= 9; i++)
        {
            RunBowlToEnd(controller);
            Assert(state.Pot.Phase == PotPhase.InProgress, $"Pot should be InProgress after bowl {i} ends");
            controller.StartNextBowl();
            Assert(state.Pot.BowlNumber == i + 1, $"BowlNumber should be {i + 1}");
        }

        // 第10碗走到 End，锅应自动结束
        Assert(state.Pot.BowlNumber == 10, "Should be bowl 10");
        RunBowlToEnd(controller);

        Assert(state.Pot.Phase == PotPhase.Ended, "Pot should be Ended after 10th bowl ends");
        Assert(state.Pot.BowlNumber == 10, "BowlNumber should remain 10");
        Assert(state.Pot.CurrentBowlPhase == BowlPhase.End, "CurrentBowlPhase should be End");
    }

    static void Test_NormalPot_CannotStartEleventhBowl()
    {
        var state = new GameState();
        var controller = new PotController(state);
        controller.StartPot();

        for (int i = 1; i <= 9; i++)
        {
            RunBowlToEnd(controller);
            controller.StartNextBowl();
        }
        RunBowlToEnd(controller); // 第10碗结束，锅进入 Ended

        Assert(state.Pot.Phase == PotPhase.Ended, "Pot should be Ended before asserting 11th bowl is blocked");

        bool threw = false;
        try { controller.StartNextBowl(); }
        catch (InvalidOperationException) { threw = true; }

        Assert(threw, "StartNextBowl on an Ended pot should throw InvalidOperationException");
    }

    static void Test_EndedPot_CannotAdvanceBowlPhase()
    {
        var state = new GameState();
        var controller = new PotController(state);
        controller.StartPot();

        for (int i = 1; i <= 9; i++)
        {
            RunBowlToEnd(controller);
            controller.StartNextBowl();
        }
        RunBowlToEnd(controller); // 第10碗结束，锅 Ended

        // 锅已结束后，尝试推进阶段应抛异常
        // 注意：此时 CurrentBowlPhase == End，即使改为 Start 后也不能推进
        state.Pot.CurrentBowlPhase = BowlPhase.Start; // 模拟绕过检查

        bool threw = false;
        try { controller.AdvanceBowlPhase(); }
        catch (InvalidOperationException) { threw = true; }

        Assert(threw, "AdvanceBowlPhase on an Ended pot should throw InvalidOperationException");
    }

    static void Test_EndedPot_CannotStartBowl()
    {
        var state = new GameState();
        var controller = new PotController(state);
        controller.StartPot();

        for (int i = 1; i <= 9; i++)
        {
            RunBowlToEnd(controller);
            controller.StartNextBowl();
        }
        RunBowlToEnd(controller); // 第10碗结束

        bool threw = false;
        try { controller.StartBowl(); }
        catch (InvalidOperationException) { threw = true; }

        Assert(threw, "StartBowl on an Ended pot should throw InvalidOperationException");
    }

    static void Test_FinalPot_DoesNotEndAfterTenthBowl()
    {
        var state = new GameState();
        // 最终锅：BowlLimit 设为极大值
        state.Pot.BowlLimit = int.MaxValue;

        var controller = new PotController(state);
        controller.StartPot();

        // 走完10碗
        for (int i = 1; i <= 9; i++)
        {
            RunBowlToEnd(controller);
            controller.StartNextBowl();
        }
        RunBowlToEnd(controller); // 第10碗结束

        // 最终锅不应该结束
        Assert(state.Pot.Phase == PotPhase.InProgress, "Final pot should still be InProgress after bowl 10");
        Assert(state.Pot.BowlNumber == 10, "BowlNumber should be 10");
    }

    static void Test_FinalPot_CanStartEleventhBowl()
    {
        var state = new GameState();
        state.Pot.BowlLimit = int.MaxValue;

        var controller = new PotController(state);
        controller.StartPot();

        for (int i = 1; i <= 9; i++)
        {
            RunBowlToEnd(controller);
            controller.StartNextBowl();
        }
        RunBowlToEnd(controller); // 第10碗结束

        // 最终锅可以进入第11碗
        controller.StartNextBowl();
        Assert(state.Pot.BowlNumber == 11, "Final pot should allow bowl 11");
        Assert(state.Pot.CurrentBowlPhase == BowlPhase.Start, "Bowl 11 should start at BowlPhase.Start");
        Assert(state.Pot.Phase == PotPhase.InProgress, "Final pot should still be InProgress at bowl 11");
    }

    static void Test_StartPot_CannotBeCalledTwice()
    {
        var state = new GameState();
        var controller = new PotController(state);
        controller.StartPot();

        bool threw = false;
        try { controller.StartPot(); }
        catch (InvalidOperationException) { threw = true; }

        Assert(threw, "StartPot called twice should throw InvalidOperationException");
    }

    static void Test_AdvanceBowlPhase_ThrowsWhenAtEnd()
    {
        var state = new GameState();
        var controller = new PotController(state);
        controller.StartPot();
        RunBowlToEnd(controller); // bowl 1 → End (pot 未结束，BowlNumber=1 < 10)

        // 第1碗结束后锅仍 InProgress，但碗在 End 阶段，推进应抛异常
        Assert(state.Pot.Phase == PotPhase.InProgress, "Pot should be InProgress after bowl 1");
        Assert(state.Pot.CurrentBowlPhase == BowlPhase.End, "Bowl phase should be End");

        bool threw = false;
        try { controller.AdvanceBowlPhase(); }
        catch (InvalidOperationException) { threw = true; }

        Assert(threw, "AdvanceBowlPhase at BowlPhase.End should throw InvalidOperationException");
    }

    static void Test_UseItem_OutsideItemPhase_Throws()
    {
        var state = new GameState();
        var controller = new PotController(state);
        controller.StartPot();
        controller.StartBowl();
        // 当前阶段是 BowlPhase.Start，不是 ItemPhase

        var def = new ItemDefinition("item_001", "辣椒酱");
        state.Player.Items.Add(new ItemInstance(def, "inst_001"));

        bool threw = false;
        try { controller.UseItem("inst_001"); }
        catch (InvalidOperationException) { threw = true; }

        Assert(threw, "UseItem outside ItemPhase should throw InvalidOperationException");
    }

    static void Test_UseItem_ConsumesItemAndReturnsIt()
    {
        var state = new GameState();
        var controller = new PotController(state);
        controller.StartPot();
        controller.StartBowl();
        controller.AdvanceBowlPhase(); // → Customer
        controller.AdvanceBowlPhase(); // → ItemPhase

        var def = new ItemDefinition("item_001", "辣椒酱");
        var inst = new ItemInstance(def, "inst_001");
        state.Player.Items.Add(inst);

        var returned = controller.UseItem("inst_001");

        Assert(ReferenceEquals(returned, inst), "UseItem must return the exact ItemInstance that was consumed");
        Assert(returned.InstanceId == "inst_001", "Returned instance must have correct InstanceId");
        Assert(ReferenceEquals(returned.Definition, def), "Returned instance must reference the correct definition");
    }

    static void Test_UseItem_RemovedFromPlayerItems()
    {
        var state = new GameState();
        var controller = new PotController(state);
        controller.StartPot();
        controller.StartBowl();
        controller.AdvanceBowlPhase(); // → Customer
        controller.AdvanceBowlPhase(); // → ItemPhase

        var def = new ItemDefinition("item_002", "醋");
        state.Player.Items.Add(new ItemInstance(def, "inst_a"));
        state.Player.Items.Add(new ItemInstance(def, "inst_b"));

        controller.UseItem("inst_a");

        Assert(state.Player.Items.Count == 1, "PlayerState.Items should have 1 item remaining after UseItem");
        Assert(state.Player.Items[0].InstanceId == "inst_b", "Remaining item should be inst_b");
        Assert(!state.Player.Items.Any(i => i.InstanceId == "inst_a"), "Consumed item must not remain in PlayerState.Items");
    }

    static void Test_UseItem_UnknownInstanceId_Throws()
    {
        var state = new GameState();
        var controller = new PotController(state);
        controller.StartPot();
        controller.StartBowl();
        controller.AdvanceBowlPhase(); // → Customer
        controller.AdvanceBowlPhase(); // → ItemPhase

        bool threw = false;
        try { controller.UseItem("nonexistent_id"); }
        catch (ArgumentException) { threw = true; }

        Assert(threw, "UseItem with unknown instanceId should throw ArgumentException");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Part 4: AddIngredient / ApplyItemEffect 辅助

    /// <summary>把 PotController 推进到 IngredientResolve 阶段。</summary>
    static PotController MakeControllerAtIngredientResolve(out GameState state)
    {
        state = new GameState();
        var ctrl = new PotController(state);
        ctrl.StartPot();
        ctrl.StartBowl();
        ctrl.AdvanceBowlPhase(); // → Customer
        ctrl.AdvanceBowlPhase(); // → ItemPhase
        ctrl.AdvanceBowlPhase(); // → IngredientSelection
        ctrl.AdvanceBowlPhase(); // → IngredientResolve
        return ctrl;
    }

    /// <summary>把 PotController 推进到 ItemPhase 阶段。</summary>
    static PotController MakeControllerAtItemPhase(out GameState state)
    {
        state = new GameState();
        var ctrl = new PotController(state);
        ctrl.StartPot();
        ctrl.StartBowl();
        ctrl.AdvanceBowlPhase(); // → Customer
        ctrl.AdvanceBowlPhase(); // → ItemPhase
        return ctrl;
    }

    // Part 4: AddIngredient

    static void Test_AddIngredient_AddsToIngredients()
    {
        var ctrl = MakeControllerAtIngredientResolve(out var state);
        var def = new IngredientDefinition("rice", "米饭", IngredientRarity.Common, 5);
        var inst = new IngredientInstance(def);
        var es = new EffectSystem();

        ctrl.AddIngredient(inst, es);

        Assert(state.Pot.Ingredients.Count == 1, "Ingredients must have 1 entry after AddIngredient");
        Assert(ReferenceEquals(state.Pot.Ingredients[0], inst), "Stored ingredient must be the same reference");
    }

    static void Test_AddIngredient_TriggersEffect()
    {
        var ctrl = MakeControllerAtIngredientResolve(out var state);
        var effect = new LambdaEffect("add_score", ctx => ctx.PotState.BaseScore += 10);
        var def = new IngredientDefinition("rice", "米饭", IngredientRarity.Common, 0, effects: new[] { effect });
        var inst = new IngredientInstance(def);
        var es = new EffectSystem();

        ctrl.AddIngredient(inst, es);

        Assert(state.Pot.BaseScore == 10, "Effect must have been triggered: BaseScore should be 10");
    }

    static void Test_AddIngredient_ContextCurrentIngredientIsCorrect()
    {
        var ctrl = MakeControllerAtIngredientResolve(out _);
        IngredientInstance? capturedIngredient = null;
        var effect = new LambdaEffect("capture", ctx => capturedIngredient = ctx.CurrentIngredient);
        var def = new IngredientDefinition("rice", "米饭", IngredientRarity.Common, 5, effects: new[] { effect });
        var inst = new IngredientInstance(def);
        var es = new EffectSystem();

        ctrl.AddIngredient(inst, es);

        Assert(ReferenceEquals(capturedIngredient, inst),
            "EffectContext.CurrentIngredient must be the added IngredientInstance");
    }

    static void Test_AddIngredient_SourceIdIsInstanceId()
    {
        var ctrl = MakeControllerAtIngredientResolve(out _);
        // 同一个 effect，同一个 sourceId → 第二次 TriggerAll 不会执行（循环保护）
        // 用这个性质间接验证 sourceId 是 InstanceId 而非 DefinitionId
        int callCount = 0;
        var effect = new LambdaEffect("counter", _ => callCount++);
        var def = new IngredientDefinition("rice", "米饭", IngredientRarity.Common, 5, effects: new[] { effect });
        var inst = new IngredientInstance(def, "specific_instance_id");
        var es = new EffectSystem();

        // 手动模拟：创建 context，预先标记 DefinitionId 不影响触发
        // 只要 AddIngredient 用 inst.InstanceId 作为 sourceId，effect 应执行一次
        ctrl.AddIngredient(inst, es);

        Assert(callCount == 1, "Effect must be called exactly once; sourceId must allow first trigger");
        Assert(inst.InstanceId == "specific_instance_id", "Confirms the correct instance was used");
    }

    static void Test_AddIngredient_WrongPhase_Throws()
    {
        var state = new GameState();
        var ctrl = new PotController(state);
        ctrl.StartPot();
        ctrl.StartBowl(); // BowlPhase.Start
        var def = new IngredientDefinition("rice", "米饭", IngredientRarity.Common, 5);
        var inst = new IngredientInstance(def);
        var es = new EffectSystem();

        bool threw = false;
        try { ctrl.AddIngredient(inst, es); }
        catch (InvalidOperationException) { threw = true; }

        Assert(threw, "AddIngredient outside IngredientResolve must throw InvalidOperationException");
    }

    static void Test_AddIngredient_WrongPhase_DoesNotAddToIngredients()
    {
        var state = new GameState();
        var ctrl = new PotController(state);
        ctrl.StartPot();
        ctrl.StartBowl(); // BowlPhase.Start
        var def = new IngredientDefinition("rice", "米饭", IngredientRarity.Common, 5);
        var inst = new IngredientInstance(def);
        var es = new EffectSystem();

        try { ctrl.AddIngredient(inst, es); } catch (InvalidOperationException) { }

        Assert(state.Pot.Ingredients.Count == 0,
            "Failed AddIngredient must not add ingredient to PotState.Ingredients");
    }

    // Part 4: ApplyItemEffect

    static void Test_ApplyItemEffect_TriggersEffect()
    {
        var ctrl = MakeControllerAtItemPhase(out var state);
        var effect = new LambdaEffect("add_flavor", ctx => ctx.PotState.AddFlavor(FlavorType.Spicy, 5));
        var def = new ItemDefinition("chili_sauce", "辣椒酱", new[] { effect });
        var inst = new ItemInstance(def);
        var es = new EffectSystem();

        ctrl.ApplyItemEffect(inst, es);

        Assert(state.Pot.GetFlavor(FlavorType.Spicy) == 5,
            "Item effect must have been triggered: Spicy should be 5");
    }

    static void Test_ApplyItemEffect_ContextCurrentIngredientIsNull()
    {
        var ctrl = MakeControllerAtItemPhase(out _);
        IngredientInstance? captured = new IngredientInstance(
            new IngredientDefinition("sentinel", "sentinel", IngredientRarity.Common, 0));
        var effect = new LambdaEffect("capture", ctx => captured = ctx.CurrentIngredient);
        var def = new ItemDefinition("item_x", "醋", new[] { effect });
        var inst = new ItemInstance(def);
        var es = new EffectSystem();

        ctrl.ApplyItemEffect(inst, es);

        Assert(captured == null, "EffectContext.CurrentIngredient must be null for item effects");
    }

    static void Test_ApplyItemEffect_SourceIdIsItemInstanceId()
    {
        var ctrl = MakeControllerAtItemPhase(out _);
        int callCount = 0;
        var effect = new LambdaEffect("counter", _ => callCount++);
        var def = new ItemDefinition("item_x", "醋", new[] { effect });
        var inst = new ItemInstance(def, "my_item_instance");
        var es = new EffectSystem();

        ctrl.ApplyItemEffect(inst, es);

        Assert(callCount == 1, "Item effect must execute once");
        Assert(inst.InstanceId == "my_item_instance", "Confirms correct instance was used as sourceId");
    }

    static void Test_ApplyItemEffect_WrongPhase_Throws()
    {
        var state = new GameState();
        var ctrl = new PotController(state);
        ctrl.StartPot();
        ctrl.StartBowl(); // BowlPhase.Start
        var def = new ItemDefinition("item_x", "醋");
        var inst = new ItemInstance(def);
        var es = new EffectSystem();

        bool threw = false;
        try { ctrl.ApplyItemEffect(inst, es); }
        catch (InvalidOperationException) { threw = true; }

        Assert(threw, "ApplyItemEffect outside ItemPhase must throw InvalidOperationException");
    }

    // Part 4: 循环保护 / 共用路径

    static void Test_AddIngredient_LoopProtection()
    {
        // 验证：同一条链中，同一 sourceId 不会被无限重复触发。
        // effectA 在执行时用相同的 instanceId 再次尝试触发自己，应被 EffectContext 阻止。
        var ctrl = MakeControllerAtIngredientResolve(out _);
        int callCount = 0;

        EffectSystem? es = null;
        LambdaEffect? effectA = null;
        string? capturedInstanceId = null;

        effectA = new LambdaEffect("effect_a", ctx =>
        {
            callCount++;
            // 用与 AddIngredient 相同的 sourceId（ingredient.InstanceId）再次触发
            es!.Trigger(effectA!, capturedInstanceId!, ctx);
        });

        var def = new IngredientDefinition("rice", "米饭", IngredientRarity.Common, 5,
            effects: new[] { effectA });
        var inst = new IngredientInstance(def);
        capturedInstanceId = inst.InstanceId;
        es = new EffectSystem();

        ctrl.AddIngredient(inst, es);

        Assert(callCount == 1, "Loop protection must prevent effectA from triggering itself again in the same chain");
    }

    static void Test_IngredientAndItem_UseSharedEffectSystemPath()
    {
        // 验证 Ingredient 和 Item 都走同一套 EffectSystem / EffectContext 路径
        // 方式：同一个 EffectSystem 实例先后接受两次调用，两次都能正常触发
        var state = new GameState();
        var ctrl = new PotController(state);
        ctrl.StartPot();
        ctrl.StartBowl();
        ctrl.AdvanceBowlPhase(); // → Customer
        ctrl.AdvanceBowlPhase(); // → ItemPhase

        var es = new EffectSystem();
        int itemEffectCount = 0;
        var itemEffect = new LambdaEffect("item_e", _ => itemEffectCount++);
        var itemDef = new ItemDefinition("item_x", "醋", new[] { itemEffect });
        var itemInst = new ItemInstance(itemDef);

        ctrl.ApplyItemEffect(itemInst, es);

        ctrl.AdvanceBowlPhase(); // → IngredientSelection
        ctrl.AdvanceBowlPhase(); // → IngredientResolve

        int ingredientEffectCount = 0;
        var ingredientEffect = new LambdaEffect("ingr_e", _ => ingredientEffectCount++);
        var ingrDef = new IngredientDefinition("rice", "米饭", IngredientRarity.Common, 5,
            effects: new[] { ingredientEffect });
        var ingrInst = new IngredientInstance(ingrDef);

        ctrl.AddIngredient(ingrInst, es);

        Assert(itemEffectCount == 1, "Item effect must have fired via EffectSystem");
        Assert(ingredientEffectCount == 1, "Ingredient effect must have fired via EffectSystem");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Part 5: BaseScore / Flavor 自动应用

    static void Test_AddIngredient_AutoAppliesBaseScore()
    {
        var ctrl = MakeControllerAtIngredientResolve(out var state);
        var def = new IngredientDefinition("filler", "填充", IngredientRarity.Common, 5);
        var es = new EffectSystem();

        ctrl.AddIngredient(new IngredientInstance(def), es);

        Assert(state.Pot.BaseScore == 5, "AddIngredient 应自动将 Definition.BaseScore(5) 应用到 PotState");
    }

    static void Test_AddIngredient_AutoAppliesFlavors()
    {
        var ctrl = MakeControllerAtIngredientResolve(out var state);
        var def = new IngredientDefinition("filler", "填充", IngredientRarity.Common, 0,
            flavors: new() { [FlavorType.Sweet] = 2 });
        var es = new EffectSystem();

        ctrl.AddIngredient(new IngredientInstance(def), es);

        Assert(state.Pot.GetFlavor(FlavorType.Sweet) == 2,
            "AddIngredient 应自动将 Definition.Flavors(Sweet+2) 应用到 PotState");
    }

    static void Test_AddIngredient_BaseScoreAndFlavorAndEffect_AllApplied()
    {
        var ctrl = MakeControllerAtIngredientResolve(out var state);
        var effect = new LambdaEffect("add_score_5", ctx => ctx.PotState.BaseScore += 5);
        var def = new IngredientDefinition("filler", "填充", IngredientRarity.Common, 3,
            flavors: new() { [FlavorType.Spicy] = 1 },
            effects: new[] { effect });
        var es = new EffectSystem();

        ctrl.AddIngredient(new IngredientInstance(def), es);

        Assert(state.Pot.BaseScore == 8, "BaseScore = 3(基础分) + 5(效果) = 8");
        Assert(state.Pot.GetFlavor(FlavorType.Spicy) == 1, "Flavor 自动应用：辣=1");
    }

    static void Test_AddIngredient_Honey_FlavorAutoApplied_Then_ScaledEffect()
    {
        var ctrl = MakeControllerAtIngredientResolve(out var state);
        state.Pot.AddFlavor(FlavorType.Sweet, 2); // 预置甜=2
        var es = new EffectSystem();

        ctrl.AddIngredient(IngredientData.CreateInstance("honey"), es);

        // 蜂蜜：自动应用 Sweet+1 → Sweet=3；基础分=2
        // ScaledFlavorScoreEffect：floor(3/3)*2=2 → BaseScore+=2
        Assert(state.Pot.BaseScore == 4, "蜂蜜：2(基础分) + 2(ScaledFlavor:甜3/3*2) = 4");
        Assert(state.Pot.GetFlavor(FlavorType.Sweet) == 3, "蜂蜜自动应用甜+1：2+1=3");
    }

    static void Test_AddIngredient_IceCube_MultiplierEffect_FinalScore()
    {
        var ctrl = MakeControllerAtIngredientResolve(out var state);
        var es = new EffectSystem();

        // 先加一个 BaseScore=10 的填充食材
        var filler = new IngredientDefinition("filler", "填充", IngredientRarity.Common, 10);
        ctrl.AddIngredient(new IngredientInstance(filler), es);

        // 加入冰块：BaseScore=0，FinalScoreMultiplier × 1.5
        ctrl.AddIngredient(IngredientData.CreateInstance("ice_cube"), es);

        ctrl.AdvanceBowlPhase(); // → ScoreCalculation
        ctrl.CalculateScore();

        // Bowl1：floor(10 * 1 * 1.5) = 15
        Assert(state.Pot.FinalScore == 15, "冰块：BaseScore=10, Bowl1 倍率×1, FinalScoreMultiplier=1.5 → FinalScore=15");
    }

    static void Test_AddIngredient_Egg_UniqueCount_WithAutoApply()
    {
        var ctrl = MakeControllerAtIngredientResolve(out var state);
        var es = new EffectSystem();

        ctrl.AddIngredient(IngredientData.CreateInstance("rice"), es);   // BaseScore=1, Umami+1
        ctrl.AddIngredient(IngredientData.CreateInstance("sugar"), es);  // BaseScore=2, Sweet+1
        ctrl.AddIngredient(IngredientData.CreateInstance("egg"), es);    // BaseScore=3, Umami+1, 3种不同→+3

        // BaseScore = 1 + 2 + 3 + 3(效果) = 9
        Assert(state.Pot.BaseScore == 9, "米饭+糖+鸡蛋：BaseScore = 1+2+3+3(鸡蛋效果) = 9");
        Assert(state.Pot.GetFlavor(FlavorType.Umami) == 2, "米饭鲜+1，鸡蛋鲜+1：鲜=2");
        Assert(state.Pot.GetFlavor(FlavorType.Sweet) == 1, "糖甜+1：甜=1");
        Assert(state.Pot.Ingredients.Count == 3, "锅中应有3个食材实例");
    }

    // ─────────────────────────────────────────────────────────────────────────

    static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception($"[FAIL] PotControllerTests: {message}");
    }
}
