using SevenSpices.Core.Companions;
using SevenSpices.Core.Content;
using SevenSpices.Core.Game;
using SevenSpices.Core.Items;

namespace SevenSpices.Tests.Game;

/// <summary>
/// 缺口 3 / 4：重开新局（脏状态重置）+ MetaState 注入 + 仙丹粉末跨局回收 / 发放。
/// 覆盖：反复 StartNewGame 幂等、金币 / 食材篮 / 道具 / 伙伴 / 锅底全部重置、
/// 局外 1 份仙丹粉末的回收再发放，以及用掉 / 未用两种情形。
/// </summary>
public static class RestartGameTests
{
    public static void RunAll()
    {
        Test_StartNewGame_ResetsDirtyState();
        Test_StartNewGame_Twice_NoResidue();
        Test_StartNewGame_WithMetaPowder_GrantsToItems();
        Test_StartNewGame_AfterPowderUsed_NoGrant();
        Test_StartNewGame_UnusedPowder_RecycledAndRegranted();

        Console.WriteLine("All RestartGameTests passed.");
    }

    // ── 辅助 ─────────────────────────────────────────────────────────────────

    static GameController MakeGame(MetaState meta) =>
        new(metaState: meta);

    static bool HasPowder(GameController gc) =>
        gc.Items.Any(i => i.Definition.Id == ItemData.ImmortalPowderId);

    static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception($"[FAIL] RestartGameTests: {message}");
    }

    // ── 缺口 3：真新局 ───────────────────────────────────────────────────────

    /// <summary>改 Gold、塞 Basket/Items/Companions、设 Bottom 味道后重开，全部回到干净初始态。</summary>
    static void Test_StartNewGame_ResetsDirtyState()
    {
        var meta = new MetaState();
        var gc = MakeGame(meta);
        gc.StartNewGame();

        // 制造脏状态
        gc.Player.Gold = 999;
        gc.Player.IngredientBasket.Clear();
        gc.Player.IngredientBasket.Add(IngredientData.CreateInstance("sugar"));
        gc.Player.Items.Clear();
        gc.Player.Items.Add(ItemData.CreateInstance("salt"));
        gc.Player.Companions.Add(new CompanionInstance(CompanionData.SweetBossCompanion));
        gc.State.Bottom.SetFlavor(FlavorType.Spicy, 7);

        gc.StartNewGame();

        Assert(gc.Player.Gold == 0, $"重开后金币应为 0，实际 {gc.Player.Gold}");
        Assert(gc.Player.Companions.Count == 0, "重开后伙伴应为空");
        Assert(gc.State.Bottom.Flavors.Count == 0,
            $"重开后锅底应为空，实际 {gc.State.Bottom.Flavors.Count} 种味道");
        Assert(gc.State.Bottom.GetFlavor(FlavorType.Spicy) == 0, "重开后锅底辣味应为 0");

        // 初始食材篮：5 米饭 + 1 个默认职业（酸·江湖野厨）专属食材
        Assert(gc.Player.IngredientBasket.Count == 6,
            $"初始食材篮应为 6 个，实际 {gc.Player.IngredientBasket.Count}");
        Assert(gc.Player.IngredientBasket.Count(i => i.Definition.Id == "rice") == 5,
            "初始食材篮应有 5 个米饭");
        Assert(gc.Player.IngredientBasket.Count(i => i.Definition.Id == "pickled_bamboo") == 1,
            "初始食材篮应有 1 个默认职业专属食材（陈年酸笋）");

        // 初始道具：默认职业专属道具（蚀味引子），不进正式随机 Registry
        Assert(gc.Player.Items.Count == 1, $"初始道具应为 1 个，实际 {gc.Player.Items.Count}");
        Assert(!HasPowder(gc), "无局外粉末时初始道具不应包含仙丹粉末");
        Assert(gc.Player.Items[0].Definition.Id == ItemData.EtchingPrimer.Id,
            "初始道具应为默认职业专属道具（蚀味引子）");
        Assert(!ItemData.Registry.GetAll().Any(d => d.Id == gc.Player.Items[0].Definition.Id),
            "职业专属道具不应属于正式随机 Registry");
    }

    /// <summary>连续两次 StartNewGame 不残留上一局的资源。</summary>
    static void Test_StartNewGame_Twice_NoResidue()
    {
        var meta = new MetaState();
        var gc = MakeGame(meta);
        gc.StartNewGame();

        // 第一局玩到一半：获得资源
        gc.Player.Gold = 42;
        gc.Player.IngredientBasket.Add(IngredientData.CreateInstance("honey"));
        gc.Player.Items.Add(ItemData.CreateInstance("msg"));
        gc.Player.Companions.Add(new CompanionInstance(CompanionData.GenerousGuestCompanion));
        gc.State.Bottom.SetFlavor(FlavorType.Sweet, 5);

        gc.StartNewGame();
        int basketAfterFirst = gc.Player.IngredientBasket.Count;
        int itemsAfterFirst = gc.Player.Items.Count;

        gc.StartNewGame();

        Assert(gc.Player.Gold == 0, "第二次重开后金币应为 0");
        Assert(gc.Player.Companions.Count == 0, "第二次重开后伙伴应为空");
        Assert(gc.State.Bottom.Flavors.Count == 0, "第二次重开后锅底应为空");
        Assert(gc.Player.IngredientBasket.Count == basketAfterFirst,
            "第二次重开后食材篮数量不应增加");
        Assert(gc.Player.IngredientBasket.Count == 6, "食材篮应稳定为 6 个初始食材");
        Assert(gc.Player.Items.Count == itemsAfterFirst, "第二次重开后道具数量不应增加");
        Assert(gc.Player.Items.Count == 1, "道具应稳定为 1 个初始道具");
    }

    // ── 缺口 4：仙丹粉末 ─────────────────────────────────────────────────────

    /// <summary>局外有 1 份粉末：重开后进入本局道具栏，局外归零。</summary>
    static void Test_StartNewGame_WithMetaPowder_GrantsToItems()
    {
        var meta = new MetaState();
        var powder = ItemData.CreateImmortalPowder();
        Assert(meta.TryAddImmortalPowder(powder), "前置：应能加入 1 份粉末");
        Assert(meta.ImmortalPowderCount == 1, "前置：局外应有 1 份粉末");

        var gc = MakeGame(meta);
        gc.StartNewGame();

        Assert(meta.ImmortalPowderCount == 0, "发放后局外粉末数应为 0");
        Assert(HasPowder(gc), "本局道具栏应包含仙丹粉末");
        Assert(gc.Items.Any(i => ReferenceEquals(i, powder)),
            "发放的应是局外保有的同一份粉末实例");
    }

    /// <summary>把发放的粉末用掉后重开：道具栏不再含粉末，局外仍为 0。</summary>
    static void Test_StartNewGame_AfterPowderUsed_NoGrant()
    {
        var meta = new MetaState();
        meta.TryAddImmortalPowder(ItemData.CreateImmortalPowder());

        var gc = MakeGame(meta);
        gc.StartNewGame();

        var powder = gc.Items.First(i => i.Definition.Id == ItemData.ImmortalPowderId);
        Assert(gc.CanUseItem, "前置：新局处于可用道具阶段");
        gc.UseItem(powder.InstanceId);
        Assert(!HasPowder(gc), "用掉后本局道具栏不应再有仙丹粉末");

        gc.StartNewGame();

        Assert(!HasPowder(gc), "用掉后重开，道具栏不应再含仙丹粉末");
        Assert(meta.ImmortalPowderCount == 0, "用掉后重开，局外粉末数应保持 0");
    }

    /// <summary>未使用粉末时重开：回收再发放，道具栏仍含粉末，局外仍为 0。</summary>
    static void Test_StartNewGame_UnusedPowder_RecycledAndRegranted()
    {
        var meta = new MetaState();
        var powder = ItemData.CreateImmortalPowder();
        meta.TryAddImmortalPowder(powder);

        var gc = MakeGame(meta);
        gc.StartNewGame();
        Assert(HasPowder(gc), "前置：首次重开后应含粉末");

        gc.StartNewGame();

        Assert(HasPowder(gc), "未使用时再次重开，道具栏仍应含仙丹粉末（回收再发放）");
        Assert(meta.ImmortalPowderCount == 0, "回收再发放后局外粉末数应仍为 0");
        Assert(gc.Items.Count(i => i.Definition.Id == ItemData.ImmortalPowderId) == 1,
            "仙丹粉末不应重复累计");
    }
}
