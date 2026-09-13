namespace SevenSpices.Core.Shop;

/// <summary>商店报价的种类：食材 / 道具（设计文档 §18）。</summary>
public enum ShopOfferKind
{
    /// <summary>食材报价，购买后进入食材篮。</summary>
    Ingredient,

    /// <summary>道具报价，购买后进入玩家道具栏。</summary>
    Item,
}
