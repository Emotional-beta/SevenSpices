using SevenSpices.Core.Ingredients;
using SevenSpices.Core.Items;

namespace SevenSpices.Core.Shop;

/// <summary>
/// 商店中的一条报价（设计文档 §18）。
/// 一条报价要么是食材、要么是道具，二者互斥；<see cref="Price"/> 为购买所需金币，
/// <see cref="IsPurchased"/> 标记该件是否已被购买（每件限购一次）。
/// 纯 C#，不依赖 Godot；表现层只读并用 <see cref="DisplayName"/> 展示名称。
/// </summary>
public class ShopOffer
{
    /// <summary>报价种类。</summary>
    public ShopOfferKind Kind { get; }

    /// <summary>食材报价的实例；道具报价时为 null。</summary>
    public IngredientInstance? Ingredient { get; }

    /// <summary>道具报价的实例；食材报价时为 null。</summary>
    public ItemInstance? Item { get; }

    /// <summary>购买该件所需金币。</summary>
    public int Price { get; }

    /// <summary>该件是否已购买（购买后置位，不允许重复购买）。</summary>
    public bool IsPurchased { get; private set; }

    /// <summary>创建一条食材报价。</summary>
    public ShopOffer(IngredientInstance ingredient, int price)
    {
        Ingredient = ingredient ?? throw new ArgumentNullException(nameof(ingredient));
        Item = null;
        Kind = ShopOfferKind.Ingredient;
        Price = ValidatePrice(price);
    }

    /// <summary>创建一条道具报价。</summary>
    public ShopOffer(ItemInstance item, int price)
    {
        Item = item ?? throw new ArgumentNullException(nameof(item));
        Ingredient = null;
        Kind = ShopOfferKind.Item;
        Price = ValidatePrice(price);
    }

    /// <summary>报价展示名称（食材名或道具名）。</summary>
    public string DisplayName => Kind == ShopOfferKind.Ingredient
        ? Ingredient!.Definition.Name
        : Item!.Definition.Name;

    /// <summary>标记该件已购买。</summary>
    public void MarkPurchased() => IsPurchased = true;

    private static int ValidatePrice(int price)
    {
        if (price < 0)
            throw new ArgumentOutOfRangeException(nameof(price), price, "Price must be non-negative.");
        return price;
    }
}
