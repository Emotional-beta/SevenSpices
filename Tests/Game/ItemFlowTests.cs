using SevenSpices.Core.Content;
using SevenSpices.Core.Effects;
using SevenSpices.Core.Game;
using SevenSpices.Core.Items;
using SevenSpices.Core.Pot;
using SevenSpices.Core.Run;

namespace SevenSpices.Tests.Game;

/// <summary>
/// A5 道具系统流程测试。
/// 覆盖：开局随机 1 个道具、IngredientSelection/ItemPhase 使用、每碗无限用、消耗语义、
/// TotalBaseScore 计入道具加分、最终锅禁用、稀有食客满意掉道具、跨锅保留、PotController 阶段放宽。
/// 全部使用固定种子保证可复现。
/// </summary>
public static class ItemFlowTests
{
    public static void RunAll()
    {
        Test_StartNewGame_GivesOneRandomItem();
        Test_UseItem_InIngredientSelection_AppliesEffectAndRemoves();
        Test_UseItem_Salt_AddsToTotalBaseScore();
        Test_UseItem_ConsumedOnlyOnce();
        Test_UseItem_ConsumedInstance_WithOtherItemPresent_ThrowsArgument();
        Test_UseItem_InItemPhase_Works();
        Test_FinalPot_CannotUseItem();
        Test_NoItems_CanUseItemFalse();
        Test_Item_BoostsScore_AffectsRareSatisfaction();
        Test_RareCustomerSatisfied_DropsItem();
        Test_Items_PersistAcrossPots();
        Test_PotController_ItemPhaseRelaxed();

        Console.WriteLine("All ItemFlowTests passed.");
    }

    // ── 辅助 ─────────────────────────────────────────────────────────────────

    /// <summary>关闭稀有食客，避免随机掉落干扰道具计数。</summary>
    static CustomerAppearanceConfig NoRare() =>
        new() { RareBowlNumbers = Array.Empty<int>() };

    /// <summary>用「能选就选、池空就跳」走到当前普通锅 Ended，但不解决奖励。</summary>
    static void FinishPotWithoutReward(GameController gc)
    {
        int guard = 0;
        while (gc.Pot.Phase == PotPhase.InProgress && guard++ < 500)
        {
            if (gc.CanSelectIngredient)
                gc.SelectIngredient(gc.CurrentCandidates[0].InstanceId);
            else if (gc.CanSkipBowl)
                gc.SkipBowl();
            else
                break;
        }
        Assert(gc.Pot.Phase == PotPhase.Ended, "FinishPotWithoutReward: 普通锅应已 Ended");
    }

    static void FinishPotAndResolveReward(GameController gc)
    {
        FinishPotWithoutReward(gc);
        if (gc.IsAwaitingReward)
            gc.ChooseReward(gc.RewardCandidates[0].InstanceId);
    }

    /// <summary>推进 RunController 经过全部普通锅，进入最终锅。</summary>
    static void AdvanceToFinalPot(RunController run, GameState state)
    {
        int total = RunController.ChaptersPerRun * RunController.PotsPerChapter;
        for (int i = 0; i < total; i++)
        {
            var ctrl = run.StartCurrentPot();
            for (int bowl = 1; bowl <= 10; bowl++)
            {
                ctrl.StartBowl();
                for (int s = 0; s < 9; s++) ctrl.AdvanceBowlPhase();
                if (bowl < 10) ctrl.StartNextBowl();
            }
            ctrl.ClosePot();
            run.AdvanceToNextPot();
        }
        Assert(state.Run.IsFinalPot, "AdvanceToFinalPot: 应已进入最终锅");
    }

    // ── 测试 ─────────────────────────────────────────────────────────────────

    /// <summary>1. StartNewGame 后恰好拥有 1 个道具，且来自正式 Registry。</summary>
    static void Test_StartNewGame_GivesOneRandomItem()
    {
        var gc = new GameController(new GameState(), NoRare(), new Random(1000));
        gc.StartNewGame();

        Assert(gc.Player.Items.Count == 1, $"开局应给 1 个道具，实际 {gc.Player.Items.Count}");
        Assert(gc.Items.Count == 1, "GameController.Items 应反映开局道具");

        var id = gc.Items[0].Definition.Id;
        Assert(ItemData.Registry.GetAll().Any(d => d.Id == id),
            $"开局道具应来自 ItemData.Registry，实际 id={id}");
    }

    /// <summary>2. IngredientSelection 阶段可用：味道生效、实例被移除。</summary>
    static void Test_UseItem_InIngredientSelection_AppliesEffectAndRemoves()
    {
        var gc = new GameController(new GameState(), NoRare(), new Random(1001));
        gc.StartNewGame();

        Assert(gc.Pot.CurrentBowlPhase == BowlPhase.IngredientSelection,
            "StartNewGame 后应处于 IngredientSelection");

        gc.Player.Items.Clear();
        var sweet = ItemData.CreateInstance("sweetener");
        gc.Player.Items.Add(sweet);

        int sweetBefore = gc.Pot.GetFlavor(FlavorType.Sweet);
        Assert(gc.CanUseItem, "IngredientSelection 且有道具时 CanUseItem 应为 true");

        gc.UseItem(sweet.InstanceId);

        Assert(gc.Pot.GetFlavor(FlavorType.Sweet) == sweetBefore + 2,
            "甜味剂应使甜 +2");
        Assert(!gc.Player.Items.Any(i => i.InstanceId == sweet.InstanceId),
            "用掉的道具应从 Items 中移除");
    }

    /// <summary>3. 食盐 +3 分同步计入 BaseScore 与本锅累计基础分 TotalBaseScore。</summary>
    static void Test_UseItem_Salt_AddsToTotalBaseScore()
    {
        var gc = new GameController(new GameState(), NoRare(), new Random(1002));
        gc.StartNewGame();

        gc.Player.Items.Clear();
        var salt = ItemData.CreateInstance("salt");
        gc.Player.Items.Add(salt);

        int baseBefore = gc.Pot.BaseScore;
        int totalBefore = gc.Pot.TotalBaseScore;

        gc.UseItem(salt.InstanceId);

        Assert(gc.Pot.BaseScore == baseBefore + 3, "食盐应使本碗 BaseScore +3");
        Assert(gc.Pot.TotalBaseScore == totalBefore + 3,
            "道具加的分数应同步计入 TotalBaseScore，避免累计漏记");
    }

    /// <summary>4. 一件道具只能使用一次：用掉后数量减 1，再用同 id 抛异常。</summary>
    static void Test_UseItem_ConsumedOnlyOnce()
    {
        var gc = new GameController(new GameState(), NoRare(), new Random(1003));
        gc.StartNewGame();

        gc.Player.Items.Clear();
        var msg = ItemData.CreateInstance("msg");
        gc.Player.Items.Add(msg);
        Assert(gc.Player.Items.Count == 1, "准备阶段应有 1 个道具");

        gc.UseItem(msg.InstanceId);
        Assert(gc.Player.Items.Count == 0, "用掉后道具数量应为 0");

        bool threw = false;
        try { gc.UseItem(msg.InstanceId); }
        catch (InvalidOperationException) { threw = true; }
        Assert(threw, "无道具时使用同一 id 应抛 InvalidOperationException");
    }

    /// <summary>
    /// 4b. 已消耗实例不可复用：玩家仍有其它道具（B），故 CanUseItem 仍为 true，
    ///     重复使用已消耗的 A 不会被 CanUseItem 拦下，而会在实例查找处抛 ArgumentException。
    /// </summary>
    static void Test_UseItem_ConsumedInstance_WithOtherItemPresent_ThrowsArgument()
    {
        var gc = new GameController(new GameState(), NoRare(), new Random(1006));
        gc.StartNewGame();

        gc.Player.Items.Clear();
        var consumed = ItemData.CreateInstance("sweetener");
        var remaining = ItemData.CreateInstance("msg");
        gc.Player.Items.Add(consumed);
        gc.Player.Items.Add(remaining);

        gc.UseItem(consumed.InstanceId);
        Assert(!gc.Player.Items.Any(i => i.InstanceId == consumed.InstanceId),
            "用掉的 A 应从 Items 中移除");

        // 仍有 B，CanUseItem 依旧为 true，因此不会在 CanUseItem 处被拦下。
        Assert(gc.CanUseItem, "仍有道具 B 时 CanUseItem 应为 true");

        bool threw = false;
        try { gc.UseItem(consumed.InstanceId); }
        catch (ArgumentException) { threw = true; }
        Assert(threw, "仍有其它道具时重复使用已消耗实例应抛 ArgumentException");

        Assert(gc.Player.Items.Any(i => i.InstanceId == remaining.InstanceId),
            "道具 B 应仍在 Player.Items 中");
    }

    /// <summary>5. ItemPhase 阶段也可使用（构造该阶段）。</summary>
    static void Test_UseItem_InItemPhase_Works()
    {
        var state = new GameState();
        var run = new RunController(state);
        run.StartRun();
        var pc = run.StartCurrentPot();
        pc.StartBowl();
        pc.AdvanceBowlPhase(); // → Customer
        pc.AdvanceBowlPhase(); // → ItemPhase

        Assert(state.Pot.CurrentBowlPhase == BowlPhase.ItemPhase, "应停在 ItemPhase");

        state.Player.Items.Add(ItemData.CreateInstance("chili_powder"));
        var gc = new GameController(state);

        int spicyBefore = state.Pot.GetFlavor(FlavorType.Spicy);
        Assert(gc.CanUseItem, "ItemPhase 且有道具时 CanUseItem 应为 true");

        gc.UseItem(state.Player.Items[0].InstanceId);

        Assert(state.Pot.GetFlavor(FlavorType.Spicy) == spicyBefore + 2,
            "辣椒粉应使辣 +2");
    }

    /// <summary>6. 最终锅禁用道具：CanUseItem 为 false，UseItem 抛异常。</summary>
    static void Test_FinalPot_CannotUseItem()
    {
        var state = new GameState();
        var run = new RunController(state);
        run.StartRun();
        AdvanceToFinalPot(run, state);

        var gc = new GameController(state);
        gc.StartCurrentPot();

        Assert(gc.IsFinalPot, "构造后应处于最终锅");
        gc.Player.Items.Add(ItemData.CreateInstance("salt"));

        Assert(!gc.CanUseItem, "最终锅 CanUseItem 应为 false");

        bool threw = false;
        try { gc.UseItem(gc.Items[0].InstanceId); }
        catch (InvalidOperationException) { threw = true; }
        Assert(threw, "最终锅 UseItem 应抛 InvalidOperationException");
    }

    /// <summary>7. 无道具时 CanUseItem 为 false。</summary>
    static void Test_NoItems_CanUseItemFalse()
    {
        var gc = new GameController(new GameState(), NoRare(), new Random(1004));
        gc.StartNewGame();

        gc.Player.Items.Clear();

        Assert(!gc.CanUseItem, "无道具时 CanUseItem 应为 false");
    }

    /// <summary>
    /// 8. 道具影响稀有食客满意度：不用道具分数不足 → 不满意；
    ///    用食盐垫高到阈值以上 → 满意。
    /// </summary>
    static void Test_Item_BoostsScore_AffectsRareSatisfaction()
    {
        // 不用道具：分数远低于 20 → 不满意
        var noSalt = RunRareBowl(saltCount: 0, seed: 2001, out _);
        Assert(noSalt.State.Customer.SatisfiedRareCustomers.Count == 0,
            "不用道具时分数不足，稀有食客不应满意");

        // 用 7 个食盐 → 本碗 BaseScore = 21 ≥ 20 → 满意
        // 走完本碗会进入下一碗并重置 BaseScore，故用 TotalBaseScore 验证道具加分。
        var withSalt = RunRareBowl(saltCount: 7, seed: 2001, out _);
        Assert(withSalt.Pot.TotalBaseScore >= 20,
            $"用食盐后本锅累计基础分应 ≥20，实际 {withSalt.Pot.TotalBaseScore}");
        Assert(withSalt.State.Customer.SatisfiedRareCustomers.Count == 1,
            "用道具垫高分数后稀有食客应满意");
    }

    /// <summary>9. 稀有食客满意时掉落 1 个随机道具（Items.Count 增加 1）。</summary>
    static void Test_RareCustomerSatisfied_DropsItem()
    {
        var gc = new GameController(
            new GameState(),
            new CustomerAppearanceConfig { RareBowlNumbers = new[] { 1 }, RareProbability = 1.0 },
            new Random(2002));
        gc.StartNewGame();

        Assert(gc.CurrentCustomer != null && gc.CurrentCustomer.Definition.IsRare,
            "第 1 碗应为稀有食客");

        gc.Player.Items.Clear();
        for (int i = 0; i < 7; i++)
            gc.Player.Items.Add(ItemData.CreateInstance("salt"));
        while (gc.CanUseItem)
            gc.UseItem(gc.Items[0].InstanceId);

        int itemsBeforeServe = gc.Items.Count;
        Assert(itemsBeforeServe == 0, "7 个食盐应已全部用掉");

        gc.SelectIngredient(gc.CurrentCandidates[0].InstanceId);

        Assert(gc.State.Customer.SatisfiedRareCustomers.Count == 1, "本次结算应判定为满意");
        Assert(gc.Items.Count == itemsBeforeServe + 1,
            $"稀有食客满意应掉落 1 个道具，实际 {gc.Items.Count}");

        var droppedId = gc.Items[gc.Items.Count - 1].Definition.Id;
        Assert(ItemData.Registry.GetAll().Any(d => d.Id == droppedId),
            $"掉落的道具应来自 ItemData.Registry，实际 id={droppedId}");
    }

    /// <summary>10. 道具为长期资源：跨锅保留，不被重置。</summary>
    static void Test_Items_PersistAcrossPots()
    {
        var gc = new GameController(new GameState(), NoRare(), new Random(1005));
        gc.StartNewGame();

        // 先消耗掉开局道具，再加一个用于跨锅追踪的道具
        gc.Player.Items.Clear();
        var salt = ItemData.CreateInstance("salt");
        gc.Player.Items.Add(salt);
        gc.UseItem(salt.InstanceId);
        Assert(gc.Items.Count == 0, "食盐应已被消耗");

        var tracked = ItemData.CreateInstance("msg");
        gc.Player.Items.Add(tracked);
        int beforeAdvance = gc.Items.Count;

        FinishPotAndResolveReward(gc);
        gc.AdvanceToNextPot();

        Assert(gc.Run.PotIndex == 2, "应进入第 2 锅");
        Assert(gc.Items.Count == beforeAdvance, "跨锅后道具数量不应变化");
        Assert(gc.Items.Any(i => i.InstanceId == tracked.InstanceId),
            "跨锅后道具实例应保留");
    }

    /// <summary>11. PotController 阶段放宽：IngredientSelection 可用，Start/ScoreLocked 仍抛。</summary>
    static void Test_PotController_ItemPhaseRelaxed()
    {
        // IngredientSelection 可用
        var state = new GameState();
        var ctrl = new PotController(state);
        ctrl.StartPot();
        ctrl.StartBowl();
        ctrl.AdvanceBowlPhase(); // → Customer
        ctrl.AdvanceBowlPhase(); // → ItemPhase
        ctrl.AdvanceBowlPhase(); // → IngredientSelection

        state.Player.Items.Add(ItemData.CreateInstance("aged_vinegar"));
        var returned = ctrl.UseItem(state.Player.Items[0].InstanceId);
        Assert(returned.Definition.Id == "aged_vinegar", "IngredientSelection 应可 UseItem");
        Assert(state.Player.Items.Count == 0, "UseItem 应消耗道具");

        ctrl.ApplyItemEffect(returned, new EffectSystem());
        Assert(state.Pot.GetFlavor(FlavorType.Sour) == 2, "陈醋应使酸 +2");

        // Start 阶段仍抛
        var stateStart = new GameState();
        var ctrlStart = new PotController(stateStart);
        ctrlStart.StartPot();
        ctrlStart.StartBowl(); // BowlPhase.Start
        stateStart.Player.Items.Add(ItemData.CreateInstance("salt"));
        bool startThrew = false;
        try { ctrlStart.UseItem(stateStart.Player.Items[0].InstanceId); }
        catch (InvalidOperationException) { startThrew = true; }
        Assert(startThrew, "Start 阶段 UseItem 应抛 InvalidOperationException");

        // ScoreLocked 阶段仍抛
        var stateLocked = new GameState();
        var ctrlLocked = new PotController(stateLocked);
        ctrlLocked.StartPot();
        ctrlLocked.StartBowl();
        for (int i = 0; i < 6; i++) ctrlLocked.AdvanceBowlPhase(); // → ScoreLocked
        Assert(stateLocked.Pot.CurrentBowlPhase == BowlPhase.ScoreLocked, "应到达 ScoreLocked");
        stateLocked.Player.Items.Add(ItemData.CreateInstance("salt"));
        bool lockedThrew = false;
        try { ctrlLocked.UseItem(stateLocked.Player.Items[0].InstanceId); }
        catch (InvalidOperationException) { lockedThrew = true; }
        Assert(lockedThrew, "ScoreLocked 阶段 UseItem 应抛 InvalidOperationException");
    }

    // ── 稀有食客辅助 ─────────────────────────────────────────────────────────

    /// <summary>
    /// 构造第 1 碗为稀有食客的对局，给出 <paramref name="saltCount"/> 个食盐并全部使用，
    /// 然后选择一个食材走完本碗，返回结算后的 GameController。
    /// </summary>
    static GameController RunRareBowl(int saltCount, int seed, out GameState state)
    {
        state = new GameState();
        var appearance = new CustomerAppearanceConfig
        {
            RareBowlNumbers = new[] { 1 },
            RareProbability = 1.0
        };
        var gc = new GameController(state, appearance, new Random(seed));
        gc.StartNewGame();

        Assert(gc.CurrentCustomer != null && gc.CurrentCustomer.Definition.IsRare,
            "第 1 碗应为稀有食客");

        gc.Player.Items.Clear();
        for (int i = 0; i < saltCount; i++)
            gc.Player.Items.Add(ItemData.CreateInstance("salt"));
        while (gc.CanUseItem)
            gc.UseItem(gc.Items[0].InstanceId);

        gc.SelectIngredient(gc.CurrentCandidates[0].InstanceId);
        return gc;
    }

    static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception($"[FAIL] ItemFlowTests: {message}");
    }
}
