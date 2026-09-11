using SevenSpices.Core.Game;

namespace SevenSpices.Core.Ingredients;

/// <summary>
/// 食材的静态定义，描述"这种食材是什么"。
/// 不代表某一份实际存在于食材池中的食材实例。
/// </summary>
public class IngredientDefinition
{
    public string Id { get; }
    public string Name { get; }
    public IngredientRarity Rarity { get; }
    public int BaseScore { get; }

    /// <summary>
    /// 食材携带的基础味道，key 为味道类型，value 为味道值。
    /// </summary>
    public IReadOnlyDictionary<FlavorType, int> Flavors { get; }

    public IngredientDefinition(
        string id,
        string name,
        IngredientRarity rarity,
        int baseScore,
        Dictionary<FlavorType, int>? flavors = null)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("IngredientDefinition Id cannot be empty.", nameof(id));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("IngredientDefinition Name cannot be empty.", nameof(name));
        if (baseScore < 0)
            throw new ArgumentOutOfRangeException(nameof(baseScore), "BaseScore cannot be negative.");

        Id = id;
        Name = name;
        Rarity = rarity;
        BaseScore = baseScore;
        Flavors = flavors != null
            ? new Dictionary<FlavorType, int>(flavors)
            : new Dictionary<FlavorType, int>();
    }
}
