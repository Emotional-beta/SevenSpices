using SevenSpices.Core.Game;

namespace SevenSpices.Core.Bottom;

/// <summary>
/// 负责锅结束后从 PotState 提炼锅底。
/// 规则（与设计文档 §12 一致）：
///   1. 只提炼「本锅最终状态中大于 0」的味道，本锅最终为 0 的味道不写入锅底；
///   2. 每种味道取 30%、向下取整、最低保留 1；
///   3. 提炼结果与旧锅底取较大值写入（锅底单调不减，旧锅底不会因为新锅提炼而下降）。
/// </summary>
public static class BottomExtractor
{
    /// <summary>
    /// 从 pot 的当前 Flavor 状态提炼锅底，结果单调合并写入 bottom。
    /// 不修改 pot，也不会让 bottom 中已有的值变小。
    /// </summary>
    public static void Extract(PotState pot, BottomState bottom)
    {
        ArgumentNullException.ThrowIfNull(pot);
        ArgumentNullException.ThrowIfNull(bottom);

        foreach (FlavorType flavor in Enum.GetValues<FlavorType>())
        {
            int current = pot.GetFlavor(flavor);

            // 本锅最终 ≤ 0 → 视为该味道未在本锅出现，不写入锅底，旧锅底保持原值
            if (current <= 0) continue;

            int extracted = Math.Max(1, (int)Math.Floor(current * 0.3));
            int oldBottom = bottom.GetFlavor(flavor);

            // 锅底单调不减：提炼值低于旧值时保留旧值
            bottom.SetFlavor(flavor, Math.Max(oldBottom, extracted));
        }
    }
}
