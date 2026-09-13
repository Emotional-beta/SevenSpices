namespace SevenSpices.Core.Common;

/// <summary>
/// 可选权重随机选取的公共实现（路线系统「餐饮风潮」的硬前置）。
/// <para>
/// 约定：<paramref name="weightSelector"/> 为 null 或权重全等时视为「均匀」，
/// 调用方必须走原有的 <see cref="Random.Next(int)"/> 路径，保证随机数消耗与旧行为逐位一致；
/// 仅当权重非全等时，才使用累积权重法（<see cref="Random.NextDouble"/>）抽取。
/// </para>
/// </summary>
internal static class WeightedRandom
{
    /// <summary>
    /// 权重是否全部相等（null 选择器或元素 ≤ 1 时视为相等）。
    /// 相等即无倾斜效果，应走均匀路径以保持既有随机数消耗。
    /// </summary>
    public static bool IsUniform<T>(IReadOnlyList<T> items, Func<T, double>? weightSelector)
    {
        if (weightSelector == null || items.Count <= 1)
            return true;

        double first = Normalize(weightSelector(items[0]));
        for (int i = 1; i < items.Count; i++)
        {
            if (Normalize(weightSelector(items[i])) != first)
                return false;
        }

        return true;
    }

    /// <summary>
    /// 按权重返回一个索引。无放回由调用方在每次抽取后移除元素自行实现。
    /// 总权重 ≤ 0（含全 0 / 非有限值）时退化为 <see cref="Random.Next(int)"/>，避免除零或死循环。
    /// </summary>
    public static int PickIndex<T>(IReadOnlyList<T> items, Func<T, double> weightSelector, Random random)
    {
        ArgumentNullException.ThrowIfNull(weightSelector);
        ArgumentNullException.ThrowIfNull(random);

        double total = 0;
        for (int i = 0; i < items.Count; i++)
            total += Normalize(weightSelector(items[i]));

        if (total <= 0)
            return random.Next(items.Count);

        double roll = random.NextDouble() * total;
        double cumulative = 0;
        for (int i = 0; i < items.Count; i++)
        {
            cumulative += Normalize(weightSelector(items[i]));
            if (roll < cumulative)
                return i;
        }

        return items.Count - 1;
    }

    /// <summary>把非法或负权重规整为 0，保证累积权重单调不减。</summary>
    private static double Normalize(double weight) =>
        double.IsFinite(weight) && weight > 0 ? weight : 0.0;
}
