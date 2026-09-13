namespace SevenSpices.Core.Content;

/// <summary>
/// 商店系统配置（设计文档 §18）。
/// 普通锅结束后出现的商店：陈列由随机食材 + 随机道具组成，食材与道具各自互不重复。
/// 本类是商店数值调优的<b>唯一入口</b>：陈列数量与单价都只改这里，
/// 不要在流程代码里分散硬编码。纯 C#，数据驱动，不依赖 Godot。
/// </summary>
public class ShopConfig
{
    private readonly int _ingredientOfferCount = 3;
    private readonly int _itemOfferCount = 2;
    private readonly int _ingredientPrice = 3;
    private readonly int _itemPrice = 2;

    /// <summary>食材陈列数量（默认 3）。超过正式 Registry 总数时返回全部；必须非负。</summary>
    public int IngredientOfferCount
    {
        get => _ingredientOfferCount;
        init
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(IngredientOfferCount), value, "IngredientOfferCount must be non-negative.");
            _ingredientOfferCount = value;
        }
    }

    /// <summary>道具陈列数量（默认 2）。超过正式 Registry 总数时返回全部；必须非负。</summary>
    public int ItemOfferCount
    {
        get => _itemOfferCount;
        init
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(ItemOfferCount), value, "ItemOfferCount must be non-negative.");
            _itemOfferCount = value;
        }
    }

    /// <summary>每件食材的售价金币（默认 3）。必须非负。</summary>
    public int IngredientPrice
    {
        get => _ingredientPrice;
        init
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(IngredientPrice), value, "IngredientPrice must be non-negative.");
            _ingredientPrice = value;
        }
    }

    /// <summary>每件道具的售价金币（默认 2）。必须非负。</summary>
    public int ItemPrice
    {
        get => _itemPrice;
        init
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(ItemPrice), value, "ItemPrice must be non-negative.");
            _itemPrice = value;
        }
    }
}
