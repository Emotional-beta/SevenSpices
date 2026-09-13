using SevenSpices.Core.Content;
using SevenSpices.Core.Events;
using SevenSpices.Core.Game;
using SevenSpices.Core.Pot;
using SevenSpices.Core.Shop;

namespace SevenSpices.Tests.Game;

/// <summary>
/// 商店系统（设计文档 §18）测试。
/// 覆盖：普通锅结束生成 3 食材 + 2 道具（各自不重复）、商店门控「进入下一锅」、
/// 购买扣金币与入账、限购一次、金币不足、跳过商店、配置可调（无商店不卡推进、单价生效）、
/// 最终锅无商店、事件发布、越界索引、道具金币不足、ItemPrice 可调、0 报价不发事件。
/// 固定种子保证可复现。
/// </summary>
public static class ShopTests
{
    public static void RunAll()
    {
        Test_PotEnd_OpensShop_WithOffers_And_BlocksAdvance();
        Test_Buy_Ingredient_And_Item_DeductsGold_And_GrantsItems();
        Test_Buy_WithInsufficientGold_Throws();
        Test_Buy_PurchasedOfferTwice_Throws();
        Test_SkipShop_Resolves_And_UnlocksAdvance();
        Test_SkipShop_PublishesSkippedEvent();
        Test_ShopConfig_ZeroOffers_NoShopAndNoBlock();
        Test_IngredientPrice_IsConfigurable();
        Test_FinalPot_NoShop();
        Test_ShopEvents_OfferedAndPurchased();
        Test_Buy_OutOfRangeIndex_Throws();
        Test_Buy_Item_WithInsufficientGold_Throws();
        Test_ItemPrice_IsConfigurable();
        Test_ZeroOffers_DoesNotPublishShopOfferedEvent();

        Console.WriteLine("All ShopTests passed.");
    }

    // ── 辅助 ─────────────────────────────────────────────────────────────────

    /// <summary>关闭稀有食客，避免随机掉落/伙伴候选干扰商店断言。</summary>
    static CustomerAppearanceConfig NoRare() =>
        new() { RareBowlNumbers = Array.Empty<int>() };

    /// <summary>
    /// 用「能选就选、池空就跳」走到当前普通锅 Ended，并结算奖励与伙伴（若存在），
    /// 但<b>不</b>结算商店，以便断言商店态。
    /// </summary>
    static void FinishPotResolveRewardAndCompanion(GameController gc)
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

        Assert(gc.Pot.Phase == PotPhase.Ended, "FinishPotResolveRewardAndCompanion: 普通锅应已 Ended");

        if (gc.IsAwaitingReward)
            gc.ChooseReward(gc.RewardCandidates[0].InstanceId);
        if (gc.IsAwaitingCompanionChoice)
            gc.SkipCompanionChoice();
    }

    /// <summary>走完普通锅并结算全部三道门控（奖励 → 伙伴 → 商店），便于连续推进多锅。</summary>
    static void FinishPotAndResolveAll(GameController gc)
    {
        FinishPotResolveRewardAndCompanion(gc);
        if (gc.IsShopOpen)
            gc.SkipShop();

        // 每章第 3 锅商店后会出现路线选择并门控推进；本辅助跳过（自动吃保底）。
        if (gc.IsAwaitingRouteChoice)
            gc.SkipRoute();
    }

    static int FirstIngredientIndex(GameController gc) =>
        gc.ShopOffers.ToList().FindIndex(o => o.Kind == ShopOfferKind.Ingredient);

    static int FirstItemIndex(GameController gc) =>
        gc.ShopOffers.ToList().FindIndex(o => o.Kind == ShopOfferKind.Item);

    // ── 测试 ─────────────────────────────────────────────────────────────────

    /// <summary>1. 普通锅结束：商店营业、5 条报价（3 食材 + 2 道具，各自不重复）、门控推进、生成不改金币。</summary>
    static void Test_PotEnd_OpensShop_WithOffers_And_BlocksAdvance()
    {
        var gc = new GameController(new GameState(), NoRare(), new Random(9001));
        gc.StartNewGame();

        FinishPotResolveRewardAndCompanion(gc);

        Assert(gc.IsShopOpen, "普通锅结束且未结算时应处于商店营业态");
        Assert(gc.CanSkipShop, "营业时可跳过");
        Assert(gc.ShopOffers.Count == 5, $"默认应陈列 5 条报价，实际 {gc.ShopOffers.Count}");

        var ingredientOffers = gc.ShopOffers.Where(o => o.Kind == ShopOfferKind.Ingredient).ToList();
        var itemOffers = gc.ShopOffers.Where(o => o.Kind == ShopOfferKind.Item).ToList();
        Assert(ingredientOffers.Count == 3, $"食材应为 3 条，实际 {ingredientOffers.Count}");
        Assert(itemOffers.Count == 2, $"道具应为 2 条，实际 {itemOffers.Count}");

        var ingredientIds = ingredientOffers.Select(o => o.Ingredient!.Definition.Id).ToList();
        Assert(ingredientIds.Distinct().Count() == ingredientIds.Count, "食材报价不得重复");
        var itemIds = itemOffers.Select(o => o.Item!.Definition.Id).ToList();
        Assert(itemIds.Distinct().Count() == itemIds.Count, "道具报价不得重复");

        Assert(ingredientOffers.All(o => o.Price == 3), "食材默认单价应为 3");
        Assert(itemOffers.All(o => o.Price == 2), "道具默认单价应为 2");

        // 10 碗普通食客 × 1 金币；商店生成本身不增删金币。
        Assert(gc.Player.Gold == 10, $"商店生成不应改变金币（应为 10），实际 {gc.Player.Gold}");
        Assert(!gc.CanAdvanceToNextPot, "商店未结算前不应可推进");
    }

    /// <summary>2. 购买：食材 -3 金币并进食材篮、道具 -2 金币并进道具栏，且限购一次。</summary>
    static void Test_Buy_Ingredient_And_Item_DeductsGold_And_GrantsItems()
    {
        var gc = new GameController(new GameState(), NoRare(), new Random(9002));
        gc.StartNewGame();
        FinishPotResolveRewardAndCompanion(gc);

        int goldBefore = gc.Player.Gold;
        int basketBefore = gc.Player.IngredientBasket.Count;
        int itemsBefore = gc.Player.Items.Count;

        int ingIndex = FirstIngredientIndex(gc);
        var ingOffer = gc.ShopOffers[ingIndex];
        Assert(gc.CanBuy(ingIndex), "金币足够时食材报价应可购买");
        gc.Buy(ingIndex);
        Assert(gc.Player.Gold == goldBefore - 3, $"购买食材应扣 3 金币，实际 {gc.Player.Gold}");
        Assert(gc.Player.IngredientBasket.Count == basketBefore + 1, "购买食材应使食材篮 +1");
        Assert(ReferenceEquals(gc.Player.IngredientBasket[^1], ingOffer.Ingredient),
            "进入食材篮的应是报价中的食材实例");
        Assert(ingOffer.IsPurchased, "购买后该食材报价应标记已购买");
        Assert(!gc.CanBuy(ingIndex), "已购买的报价不可再次购买");

        int itemIndex = FirstItemIndex(gc);
        var itemOffer = gc.ShopOffers[itemIndex];
        Assert(gc.CanBuy(itemIndex), "金币足够时道具报价应可购买");
        gc.Buy(itemIndex);
        Assert(gc.Player.Gold == goldBefore - 3 - 2, $"购买道具应再扣 2 金币，实际 {gc.Player.Gold}");
        Assert(gc.Player.Items.Count == itemsBefore + 1, "购买道具应使道具栏 +1");
        Assert(ReferenceEquals(gc.Player.Items[^1], itemOffer.Item),
            "进入道具栏的应是报价中的道具实例");
        Assert(itemOffer.IsPurchased, "购买后该道具报价应标记已购买");
    }

    /// <summary>3. 金币不足：CanBuy 为 false，Buy 抛 InvalidOperationException 且不改变金币。</summary>
    static void Test_Buy_WithInsufficientGold_Throws()
    {
        var gc = new GameController(new GameState(), NoRare(), new Random(9003));
        gc.StartNewGame();
        FinishPotResolveRewardAndCompanion(gc);

        gc.Player.Gold = 0;
        int ingIndex = FirstIngredientIndex(gc);
        Assert(!gc.CanBuy(ingIndex), "金币不足时 CanBuy 应为 false");

        bool threw = false;
        try { gc.Buy(ingIndex); }
        catch (InvalidOperationException) { threw = true; }

        Assert(threw, "金币不足时 Buy 应抛 InvalidOperationException");
        Assert(gc.Player.Gold == 0, "失败的购买不应改变金币");
        Assert(!gc.ShopOffers[ingIndex].IsPurchased, "失败的购买不应标记已购买");
    }

    /// <summary>4. 重复购买已购报价：金币充足也抛 InvalidOperationException。</summary>
    static void Test_Buy_PurchasedOfferTwice_Throws()
    {
        var gc = new GameController(new GameState(), NoRare(), new Random(9004));
        gc.StartNewGame();
        FinishPotResolveRewardAndCompanion(gc);

        gc.Player.Gold = 100;
        int ingIndex = FirstIngredientIndex(gc);
        gc.Buy(ingIndex);

        bool threw = false;
        try { gc.Buy(ingIndex); }
        catch (InvalidOperationException) { threw = true; }

        Assert(threw, "重复购买同一已购报价应抛 InvalidOperationException");
    }

    /// <summary>5. SkipShop：清空报价并解锁推进；商店未结算时 AdvanceToNextPot 抛异常。</summary>
    static void Test_SkipShop_Resolves_And_UnlocksAdvance()
    {
        var gc = new GameController(new GameState(), NoRare(), new Random(9005));
        gc.StartNewGame();
        FinishPotResolveRewardAndCompanion(gc);

        bool advanceThrew = false;
        try { gc.AdvanceToNextPot(); }
        catch (InvalidOperationException) { advanceThrew = true; }
        Assert(advanceThrew, "商店未结算时 AdvanceToNextPot 应抛 InvalidOperationException");
        Assert(gc.Run.PotIndex == 1, "抛异常后不应推进");

        gc.SkipShop();

        Assert(!gc.IsShopOpen, "跳过后不应再营业");
        Assert(gc.ShopOffers.Count == 0, "跳过后报价应清空");
        Assert(gc.CanAdvanceToNextPot, "跳过后应可推进到下一锅");
    }

    /// <summary>
    /// 5b. SkipShop 发布 ShopSkippedEvent，负载为跳过时清空的报价数；
    /// 语义回归：跳过后 CanAdvanceToNextPot 必须为 true（修复「点跳过没反应」的根因）。
    /// </summary>
    static void Test_SkipShop_PublishesSkippedEvent()
    {
        var gc = new GameController(new GameState(), NoRare(), new Random(9014));
        gc.StartNewGame();
        FinishPotResolveRewardAndCompanion(gc);

        ShopSkippedEvent? skipped = null;
        gc.Events.Subscribe<ShopSkippedEvent>(e => skipped = e);

        int offersBefore = gc.ShopOffers.Count;
        gc.SkipShop();

        Assert(skipped != null, "SkipShop 应发布 ShopSkippedEvent");
        Assert(skipped!.OfferCount == offersBefore,
            $"ShopSkippedEvent.OfferCount 应为跳过时的报价数（{offersBefore}），实际 {skipped.OfferCount}");
        Assert(gc.CanAdvanceToNextPot, "跳过商店后 CanAdvanceToNextPot 应为 true");
    }

    /// <summary>6. 配置为 0 条报价：无商店且不卡推进（商店环节自动视为已结算）。</summary>
    static void Test_ShopConfig_ZeroOffers_NoShopAndNoBlock()
    {
        var gc = new GameController(
            new GameState(), NoRare(), new Random(9006),
            new PotRewardConfig(),
            new ShopConfig { IngredientOfferCount = 0, ItemOfferCount = 0 });
        gc.StartNewGame();

        FinishPotResolveRewardAndCompanion(gc);

        Assert(!gc.IsShopOpen, "无报价时不应营业");
        Assert(gc.ShopOffers.Count == 0, "无报价时报价列表应为空");
        Assert(gc.CanAdvanceToNextPot, "无商店时应不卡推进");
    }

    /// <summary>7. IngredientPrice 可调：注入 1 → 购买食材只扣 1 金币。</summary>
    static void Test_IngredientPrice_IsConfigurable()
    {
        var gc = new GameController(
            new GameState(), NoRare(), new Random(9007),
            new PotRewardConfig(),
            new ShopConfig { IngredientPrice = 1 });
        gc.StartNewGame();
        FinishPotResolveRewardAndCompanion(gc);

        int goldBefore = gc.Player.Gold;
        int ingIndex = FirstIngredientIndex(gc);
        Assert(gc.ShopOffers[ingIndex].Price == 1, "注入单价 1 应反映到报价");
        gc.Buy(ingIndex);
        Assert(gc.Player.Gold == goldBefore - 1, $"自定义单价应生效（扣 1），实际 {gc.Player.Gold}");
    }

    /// <summary>8. 最终锅无商店：进入最终锅及结算后 IsShopOpen 均为 false。</summary>
    static void Test_FinalPot_NoShop()
    {
        var gc = new GameController(new GameState(), NoRare(), new Random(9008));
        gc.StartNewGame();

        for (int i = 0; i < 9; i++)
        {
            FinishPotAndResolveAll(gc);
            gc.AdvanceToNextPot();
        }

        Assert(gc.IsFinalPot, "9 锅普通锅后应进入最终锅");
        Assert(!gc.IsShopOpen, "最终锅进行中不应有商店");

        int guard = 0;
        while (gc.CanSelectIngredient && guard++ < 100)
            gc.SelectIngredient(gc.CurrentCandidates[0].InstanceId);

        gc.EndCooking();

        Assert(!gc.IsShopOpen, "最终锅结算后也不应有商店");
    }

    /// <summary>9. 事件：生成商店发 ShopOfferedEvent，购买发 ShopPurchasedEvent。</summary>
    static void Test_ShopEvents_OfferedAndPurchased()
    {
        var gc = new GameController(new GameState(), NoRare(), new Random(9009));
        gc.StartNewGame();

        var offered = new List<ShopOfferedEvent>();
        gc.Events.Subscribe<ShopOfferedEvent>(offered.Add);

        FinishPotResolveRewardAndCompanion(gc);

        Assert(offered.Count == 1, $"锅结束应发布一次 ShopOfferedEvent，实际 {offered.Count}");
        Assert(offered[0].Offers.Count == 5, "ShopOfferedEvent 应携带 5 条报价");
        Assert(ReferenceEquals(offered[0].Offers[0], gc.ShopOffers[0]),
            "ShopOfferedEvent 的报价应与当前报价一致");

        ShopPurchasedEvent? purchased = null;
        gc.Events.Subscribe<ShopPurchasedEvent>(e => purchased = e);

        gc.Player.Gold = 100;
        int ingIndex = FirstIngredientIndex(gc);
        gc.Buy(ingIndex);

        Assert(purchased != null, "购买应发布 ShopPurchasedEvent");
        Assert(ReferenceEquals(purchased!.Offer, gc.ShopOffers[ingIndex]),
            "ShopPurchasedEvent 应携带被购买的报价");
    }

    /// <summary>10. 越界索引：CanBuy 为 false，Buy 抛异常。</summary>
    static void Test_Buy_OutOfRangeIndex_Throws()
    {
        var gc = new GameController(new GameState(), NoRare(), new Random(9010));
        gc.StartNewGame();
        FinishPotResolveRewardAndCompanion(gc);

        gc.Player.Gold = 100;
        int count = gc.ShopOffers.Count;

        Assert(!gc.CanBuy(-1), "负索引 CanBuy 应为 false");
        Assert(!gc.CanBuy(count), "越界索引 CanBuy 应为 false");

        bool threw = false;
        try { gc.Buy(-1); }
        catch (ArgumentOutOfRangeException) { threw = true; }
        Assert(threw, "负索引 Buy 应抛 ArgumentOutOfRangeException");

        threw = false;
        try { gc.Buy(count); }
        catch (ArgumentOutOfRangeException) { threw = true; }
        Assert(threw, "越界索引 Buy 应抛 ArgumentOutOfRangeException");
    }

    /// <summary>11. 道具金币不足：CanBuy 为 false，Buy 抛 InvalidOperationException 且不改变状态。</summary>
    static void Test_Buy_Item_WithInsufficientGold_Throws()
    {
        var gc = new GameController(new GameState(), NoRare(), new Random(9011));
        gc.StartNewGame();
        FinishPotResolveRewardAndCompanion(gc);

        gc.Player.Gold = 1; // 道具默认单价 2
        int itemIndex = FirstItemIndex(gc);
        Assert(itemIndex >= 0, "应存在道具报价");
        Assert(!gc.CanBuy(itemIndex), "金币不足时道具 CanBuy 应为 false");

        bool threw = false;
        try { gc.Buy(itemIndex); }
        catch (InvalidOperationException) { threw = true; }

        Assert(threw, "金币不足时 Buy 道具应抛 InvalidOperationException");
        Assert(gc.Player.Gold == 1, "失败的购买不应改变金币");
        Assert(!gc.ShopOffers[itemIndex].IsPurchased, "失败的购买不应标记已购买");
    }

    /// <summary>12. ItemPrice 可调：注入 5 → 购买道具扣 5 金币。</summary>
    static void Test_ItemPrice_IsConfigurable()
    {
        var gc = new GameController(
            new GameState(), NoRare(), new Random(9012),
            new PotRewardConfig(),
            new ShopConfig { ItemPrice = 5 });
        gc.StartNewGame();
        FinishPotResolveRewardAndCompanion(gc);

        gc.Player.Gold = 100;
        int goldBefore = gc.Player.Gold;
        int itemIndex = FirstItemIndex(gc);
        Assert(gc.ShopOffers[itemIndex].Price == 5, "注入道具单价 5 应反映到报价");
        gc.Buy(itemIndex);
        Assert(gc.Player.Gold == goldBefore - 5, $"自定义道具单价应生效（扣 5），实际 {gc.Player.Gold}");
    }

    /// <summary>13. 0 报价：不发布 ShopOfferedEvent（未生成商店即无已开放语义）。</summary>
    static void Test_ZeroOffers_DoesNotPublishShopOfferedEvent()
    {
        var gc = new GameController(
            new GameState(), NoRare(), new Random(9013),
            new PotRewardConfig(),
            new ShopConfig { IngredientOfferCount = 0, ItemOfferCount = 0 });
        gc.StartNewGame();

        var offered = new List<ShopOfferedEvent>();
        gc.Events.Subscribe<ShopOfferedEvent>(offered.Add);

        FinishPotResolveRewardAndCompanion(gc);

        Assert(offered.Count == 0, $"0 报价不应发布 ShopOfferedEvent，实际 {offered.Count}");
    }

    static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception($"[FAIL] ShopTests: {message}");
    }
}
