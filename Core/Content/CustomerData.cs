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

    // ── 饕餮（每章末 Boss / 最终锅真身）────────────────────────────────────────
    // 满意条件是「本锅累计最终分 >= 阈值」。
    // 刻意不绑伙伴（不进伙伴候选）、不走随机掉落（赏赐由 Boss 专属流程发放）。

    /// <summary>第 1 章末饕餮：幼体。</summary>
    public static CustomerDefinition TaotieChild { get; } = BuildTaotie(BossConfig.Default.GetChapterForm(1));

    /// <summary>第 2 章末饕餮：少女。</summary>
    public static CustomerDefinition TaotieMaiden { get; } = BuildTaotie(BossConfig.Default.GetChapterForm(2));

    /// <summary>第 3 章末饕餮：御姐。</summary>
    public static CustomerDefinition TaotieLady { get; } = BuildTaotie(BossConfig.Default.GetChapterForm(3));

    /// <summary>最终锅真身：饕餮。</summary>
    public static CustomerDefinition TaotieTrue { get; } = BuildTaotie(BossConfig.Default.GetForm("taotie_true"));

    /// <summary>全部饕餮形态（3 个章末形态 + 真身），顺序与 BossConfig 一致。</summary>
    public static IReadOnlyList<CustomerDefinition> TaotieForms { get; } = new[]
    {
        TaotieChild, TaotieMaiden, TaotieLady, TaotieTrue
    };

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

    /// <summary>按形态 Id 取饕餮定义；找不到抛 <see cref="ArgumentException"/>。</summary>
    public static CustomerDefinition GetTaotieDefinition(string formId)
    {
        foreach (var definition in TaotieForms)
        {
            if (definition.Id == formId)
                return definition;
        }

        throw new ArgumentException($"Taotie form with Id '{formId}' not found.", nameof(formId));
    }

    /// <summary>按形态 Id 创建饕餮实例。</summary>
    public static CustomerInstance CreateTaotieInstance(string formId) =>
        new(GetTaotieDefinition(formId));

    /// <summary>创建第 1 章末饕餮·幼体实例。</summary>
    public static CustomerInstance CreateTaotieChildInstance() => new(TaotieChild);

    /// <summary>创建第 2 章末饕餮·少女实例。</summary>
    public static CustomerInstance CreateTaotieMaidenInstance() => new(TaotieMaiden);

    /// <summary>创建第 3 章末饕餮·御姐实例。</summary>
    public static CustomerInstance CreateTaotieLadyInstance() => new(TaotieLady);

    /// <summary>创建最终锅饕餮真身实例。</summary>
    public static CustomerInstance CreateTaotieTrueInstance() => new(TaotieTrue);

    /// <summary>
    /// 由 <see cref="BossFormConfig"/> 构建饕餮 Definition：阈值从配置取，不硬编码；
    /// 刻意不传 companionReward（= null）。
    /// </summary>
    static CustomerDefinition BuildTaotie(BossFormConfig form) => new(
        id: form.Id,
        name: form.Name,
        isRare: true,
        satisfactionConditions: new[]
        {
            new CustomerSatisfactionCondition(ConditionType.PotTotalScoreAtLeast, form.SatisfyThreshold)
        });
}
