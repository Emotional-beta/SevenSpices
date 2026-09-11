namespace SevenSpices.Core.Game;

/// <summary>
/// 锅底状态：保存上一锅提炼出的长期积累味道值。
/// 新锅开始时通过 ApplyToPot 直接注入 PotState，之后与本锅产生的味道无本质区别。
/// 锅底只在规定的提取阶段衰减，不会因进入下一锅而重复衰减。
/// </summary>
public class BottomState
{
    /// <summary>锅底保留的各味道等级。</summary>
    public Dictionary<FlavorType, int> Flavors { get; } = new();

    /// <summary>获取指定味道的锅底值，不存在则返回 0。</summary>
    public int GetFlavor(FlavorType flavor) =>
        Flavors.TryGetValue(flavor, out int v) ? v : 0;

    /// <summary>设置指定味道的锅底值（最低 0）。</summary>
    public void SetFlavor(FlavorType flavor, int value) =>
        Flavors[flavor] = Math.Max(0, value);

    /// <summary>
    /// 将锅底味道应用到新锅的 PotState。
    /// 只在锅开始时调用一次，之后锅底值不再重复注入。
    /// </summary>
    public void ApplyToPot(PotState potState)
    {
        ArgumentNullException.ThrowIfNull(potState);
        foreach (var (flavor, value) in Flavors)
        {
            if (value > 0)
                potState.AddFlavor(flavor, value);
        }
    }
}
