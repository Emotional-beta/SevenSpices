using SevenSpices.Core.Customers;

namespace SevenSpices.Core.Content;

/// <summary>
/// 食客出现机制与基础奖励的集中配置。
/// 这是后续调整食客出现机制的<b>唯一入口</b>：
/// 普通/稀有食客 Definition、稀有出现碗数、出现概率、普通食客基础金币
/// 都只修改这里，不要在流程代码里分散硬编码。
/// 纯 C#，数据驱动，不依赖 Godot。
/// </summary>
public class CustomerAppearanceConfig
{
    /// <summary>普通食客喝粥后的基础金币奖励。默认 1。</summary>
    public int BaseGoldReward { get; init; } = 1;

    /// <summary>
    /// 可能出现稀有食客的碗数（1-based）。默认第 5、7 碗（设计文档 §二十）。
    /// </summary>
    public IReadOnlyCollection<int> RareBowlNumbers { get; init; } = new[] { 5, 7 };

    /// <summary>
    /// 稀有食客在候选碗出现的概率（0~1）。默认 1.0 表示必定出现；
    /// 调低即成为概率出现。取值必须落在 [0,1]，越界在赋值时抛
    /// <see cref="ArgumentOutOfRangeException"/>（配置错误尽早暴露）。
    /// </summary>
    public double RareProbability
    {
        get => _rareProbability;
        init
        {
            if (double.IsNaN(value) || value < 0.0 || value > 1.0)
                throw new ArgumentOutOfRangeException(
                    nameof(RareProbability), value, "RareProbability must be within [0, 1].");
            _rareProbability = value;
        }
    }

    private readonly double _rareProbability = 1.0;

    /// <summary>普通食客 Definition。</summary>
    public CustomerDefinition NormalCustomer { get; init; } = CustomerData.NormalCustomer;

    /// <summary>稀有食客 Definition。</summary>
    public CustomerDefinition RareCustomer { get; init; } = CustomerData.RareCustomer;

    /// <summary>
    /// 为指定碗创建食客实例：碗数命中 <see cref="RareBowlNumbers"/>
    /// 且随机值小于 <see cref="RareProbability"/> 时返回稀有实例，否则返回普通实例。
    /// </summary>
    public CustomerInstance CreateCustomerForBowl(int bowlNumber, Random random)
    {
        ArgumentNullException.ThrowIfNull(random);

        // RareBowlNumbers 为 null 时按空集合处理，视为「本局不出现稀有食客」。
        bool rareBowl = RareBowlNumbers?.Contains(bowlNumber) == true;
        if (rareBowl && random.NextDouble() < RareProbability)
            return new CustomerInstance(RareCustomer);

        return new CustomerInstance(NormalCustomer);
    }
}
