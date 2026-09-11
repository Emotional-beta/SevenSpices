using SevenSpices.Core.Effects;

namespace SevenSpices.Core.Items;

/// <summary>
/// 道具的静态定义，描述"这种道具是什么"。
/// 不包含任何运行时状态，不执行道具效果逻辑。
/// </summary>
public class ItemDefinition
{
    public string Id { get; }
    public string Name { get; }

    /// <summary>
    /// 道具携带的效果列表。使用时由 EffectSystem 按顺序触发。
    /// </summary>
    public IReadOnlyList<IEffect> Effects { get; }

    public ItemDefinition(string id, string name, IEnumerable<IEffect>? effects = null)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("ItemDefinition Id cannot be empty.", nameof(id));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("ItemDefinition Name cannot be empty.", nameof(name));

        Id = id;
        Name = name;
        Effects = effects != null
            ? effects.ToList().AsReadOnly()
            : new List<IEffect>().AsReadOnly();
    }
}
