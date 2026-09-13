using System.Collections.ObjectModel;
using SevenSpices.Core.Items;

namespace SevenSpices.Core.Content;

/// <summary>
/// 局外（跨局）保留状态。由 <see cref="SevenSpices.Core.Save.SaveSerializer"/> 持久化到存档，随存档跨局保留。
/// 由 GameController 外部持有、新局开始时注入。
/// 当前仅承载 Boss 赏赐「仙丹粉末」，最多保留 <see cref="MaxImmortalPowder"/> 个。
/// </summary>
public class MetaState
{
    /// <summary>仙丹粉末最多保留数量。</summary>
    public const int MaxImmortalPowder = 1;

    private readonly List<ItemInstance> _immortalPowders = new();

    /// <summary>内部列表的只读包装（构造时缓存一次，避免每次访问分配）。</summary>
    private readonly ReadOnlyCollection<ItemInstance> _immortalPowdersView;

    public MetaState()
    {
        _immortalPowdersView = _immortalPowders.AsReadOnly();
    }

    /// <summary>当前持有的仙丹粉末（真正的只读视图，无法被强转回可变列表）。</summary>
    public IReadOnlyList<ItemInstance> ImmortalPowders => _immortalPowdersView;

    /// <summary>当前持有的仙丹粉末数量。</summary>
    public int ImmortalPowderCount => _immortalPowders.Count;

    /// <summary>加入 1 个仙丹粉末；已达上限时不做任何事并返回 false。</summary>
    public bool TryAddImmortalPowder(ItemInstance powder)
    {
        ArgumentNullException.ThrowIfNull(powder);

        if (_immortalPowders.Count >= MaxImmortalPowder)
            return false;

        _immortalPowders.Add(powder);
        return true;
    }

    /// <summary>取出（消耗）1 个仙丹粉末；没有时返回 null。</summary>
    public ItemInstance? ConsumeImmortalPowder()
    {
        if (_immortalPowders.Count == 0)
            return null;

        int lastIndex = _immortalPowders.Count - 1;
        var powder = _immortalPowders[lastIndex];
        _immortalPowders.RemoveAt(lastIndex);
        return powder;
    }
}
