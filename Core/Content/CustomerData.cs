using SevenSpices.Core.Customers;
using SevenSpices.Core.Game;

namespace SevenSpices.Core.Content;

/// <summary>
/// 最小正式食客数据工厂，提供普通食客、通用稀有食客与具名稀有食客的 Definition。
/// 奖励内容（金币、食材、道具）由调用方在 EndCooking / EvaluateAndReward 时传入。
/// 具名稀有食客额外绑定一个伙伴（满意后进入本锅结束后的伙伴候选）。
/// </summary>
public static class CustomerData
{
    /// <summary>普通食客：无满意条件，喝粥后给基础金币。</summary>
    public static CustomerDefinition NormalCustomer { get; } = new(
        id: "normal_customer",
        name: "普通食客",
        isRare: false);

    /// <summary>通用稀有食客：分数 ≥ 20 或甜味 ≥ 5 时满意，不绑定伙伴（回退 / 既有测试用）。</summary>
    public static CustomerDefinition RareCustomer { get; } = new(
        id: "rare_customer",
        name: "稀有食客",
        isRare: true,
        satisfactionConditions: new[]
        {
            new CustomerSatisfactionCondition(ConditionType.ScoreAtLeast, threshold: 20),
            new CustomerSatisfactionCondition(ConditionType.FlavorAtLeast, threshold: 5, flavorTarget: FlavorType.Sweet)
        });

    /// <summary>甜心老板（具名稀有）：甜味 ≥ 5 时满意，满意后可伙伴化为「甜心老板」。</summary>
    public static CustomerDefinition SweetBossCustomer { get; } = new(
        id: "sweet_boss_customer",
        name: "甜心老板",
        isRare: true,
        satisfactionConditions: new[]
        {
            new CustomerSatisfactionCondition(ConditionType.FlavorAtLeast, threshold: 5, flavorTarget: FlavorType.Sweet)
        },
        companionReward: CompanionData.SweetBossCompanion);

    /// <summary>豪爽客（具名稀有）：分数 ≥ 20 时满意，满意后可伙伴化为「豪爽客」。</summary>
    public static CustomerDefinition GenerousGuestCustomer { get; } = new(
        id: "generous_guest_customer",
        name: "豪爽客",
        isRare: true,
        satisfactionConditions: new[]
        {
            new CustomerSatisfactionCondition(ConditionType.ScoreAtLeast, threshold: 20)
        },
        companionReward: CompanionData.GenerousGuestCompanion);

    /// <summary>创建新的普通食客实例。</summary>
    public static CustomerInstance CreateNormalInstance() =>
        new(NormalCustomer);

    /// <summary>创建新的通用稀有食客实例。</summary>
    public static CustomerInstance CreateRareInstance() =>
        new(RareCustomer);

    /// <summary>创建新的甜心老板实例。</summary>
    public static CustomerInstance CreateSweetBossInstance() =>
        new(SweetBossCustomer);

    /// <summary>创建新的豪爽客实例。</summary>
    public static CustomerInstance CreateGenerousGuestInstance() =>
        new(GenerousGuestCustomer);
}
