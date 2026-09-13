using SevenSpices.Core.Companions;
using SevenSpices.Core.Ingredients;
using SevenSpices.Core.Items;

namespace SevenSpices.Core.Game;

/// <summary>
/// 玩家长期持有的资源状态：食材篮、金币、道具、伙伴。
/// Phase 1 先实现食材篮和金币，道具/伙伴留空列表占位。
/// </summary>
public class PlayerState
{
    /// <summary>玩家当前持有的食材实例（食材篮，无容量限制）。</summary>
    public List<IngredientInstance> IngredientBasket { get; } = new();

    /// <summary>当前金币。</summary>
    public int Gold { get; set; }

    /// <summary>玩家当前持有的道具实例。</summary>
    public List<ItemInstance> Items { get; } = new();

    /// <summary>玩家持有的伙伴实例（长期资源，跨碗/跨锅保留）。</summary>
    public List<CompanionInstance> Companions { get; } = new();
}
