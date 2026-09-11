using SevenSpices.Core.Customers;
using SevenSpices.Core.Game;

namespace SevenSpices.Core.Content;

/// <summary>
/// 最小正式食客数据工厂，提供普通食客和稀有食客的 Definition。
/// 奖励内容（金币、食材、道具）由调用方在 EndCooking / EvaluateAndReward 时传入。
/// </summary>
public static class CustomerData
{
    /// <summary>普通食客：无满意条件，喝粥后给基础金币。</summary>
    public static CustomerDefinition NormalCustomer { get; } = new(
        id: "normal_customer",
        name: "普通食客",
        isRare: false);

    /// <summary>稀有食客：分数 ≥ 20 或甜味 ≥ 5 时满意。</summary>
    public static CustomerDefinition RareCustomer { get; } = new(
        id: "rare_customer",
        name: "稀有食客",
        isRare: true,
        satisfactionConditions: new[]
        {
            new CustomerSatisfactionCondition(ConditionType.ScoreAtLeast, threshold: 20),
            new CustomerSatisfactionCondition(ConditionType.FlavorAtLeast, threshold: 5, flavorTarget: FlavorType.Sweet)
        });

    /// <summary>创建新的普通食客实例。</summary>
    public static CustomerInstance CreateNormalInstance() =>
        new(NormalCustomer);

    /// <summary>创建新的稀有食客实例。</summary>
    public static CustomerInstance CreateRareInstance() =>
        new(RareCustomer);
}
