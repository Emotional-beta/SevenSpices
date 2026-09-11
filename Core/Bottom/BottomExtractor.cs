using SevenSpices.Core.Game;

namespace SevenSpices.Core.Bottom;

/// <summary>
/// 负责锅结束后从 PotState 提炼锅底：每种 Flavor 取 30%，Floor，最低保留 1，覆盖写入 BottomState。
/// </summary>
public static class BottomExtractor
{
    /// <summary>
    /// 从 pot 的当前 Flavor 状态提炼锅底，结果覆盖写入 bottom。
    /// 不修改 pot，不累加 bottom 的旧值。
    /// </summary>
    public static void Extract(PotState pot, BottomState bottom)
    {
        ArgumentNullException.ThrowIfNull(pot);
        ArgumentNullException.ThrowIfNull(bottom);

        foreach (FlavorType flavor in Enum.GetValues<FlavorType>())
        {
            int current = pot.GetFlavor(flavor);
            int extracted = Math.Max(1, (int)Math.Floor(current * 0.3));
            bottom.SetFlavor(flavor, extracted);
        }
    }
}
