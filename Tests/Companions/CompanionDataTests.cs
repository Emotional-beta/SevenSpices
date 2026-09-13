using SevenSpices.Core.Companions;
using SevenSpices.Core.Content;
using SevenSpices.Core.Customers;
using SevenSpices.Core.Game;

namespace SevenSpices.Tests.Companions;

/// <summary>
/// 伙伴内容数据测试：两个正式伙伴、Registry、具名稀有食客的满意条件与伙伴绑定，
/// 以及 CustomerAppearanceConfig 的稀有食客池行为。
/// </summary>
public static class CompanionDataTests
{
    public static void RunAll()
    {
        Test_SweetBossCompanion_Definition();
        Test_GenerousGuestCompanion_Definition();
        Test_Registry_GetAllAndGetById();
        Test_Registry_DuplicateId_Throws();
        Test_Registry_UnknownId_Throws();
        Test_SweetBossCustomer_BindsCompanion_AndCondition();
        Test_GenerousGuestCustomer_BindsCompanion_AndCondition();
        Test_NormalAndGenericRare_HaveNoCompanionReward();
        Test_AppearanceConfig_DefaultRareCustomers_ContainsBothNamed();
        Test_AppearanceConfig_EmptyRareCustomers_FallsBackToNormal();
        Test_AppearanceConfig_UsesInjectedRareCustomer();

        Console.WriteLine("All CompanionDataTests passed.");
    }

    /// <summary>甜心老板：Id/Name 正确，且带一个 E2 hook。</summary>
    static void Test_SweetBossCompanion_Definition()
    {
        var def = CompanionData.SweetBossCompanion;
        Assert(def.Id == "sweet_boss", "甜心老板 Id 应为 sweet_boss");
        Assert(def.Name == "甜心老板", "甜心老板 Name 应为「甜心老板」");
        Assert(def.Hooks.Count == 1, "甜心老板应恰好有 1 个 hook");
        Assert(def.Hooks[0] is IBottomSettlementHook, "甜心老板的 hook 应为 E2（IBottomSettlementHook）");
        Assert(def.Hooks[0] is not IIngredientBaseScoreModifier, "甜心老板不应实现 E1");
    }

    /// <summary>豪爽客：Id/Name 正确，且带一个 E1 hook。</summary>
    static void Test_GenerousGuestCompanion_Definition()
    {
        var def = CompanionData.GenerousGuestCompanion;
        Assert(def.Id == "generous_guest", "豪爽客 Id 应为 generous_guest");
        Assert(def.Name == "豪爽客", "豪爽客 Name 应为「豪爽客」");
        Assert(def.Hooks.Count == 1, "豪爽客应恰好有 1 个 hook");
        Assert(def.Hooks[0] is IIngredientBaseScoreModifier, "豪爽客的 hook 应为 E1（IIngredientBaseScoreModifier）");
    }

    static void Test_Registry_GetAllAndGetById()
    {
        var all = CompanionData.Registry.GetAll();
        Assert(all.Count == 2, $"正式 Registry 应含 2 个伙伴，实际 {all.Count}");
        Assert(ReferenceEquals(CompanionData.Registry.Get("sweet_boss"), CompanionData.SweetBossCompanion),
            "Registry.Get(sweet_boss) 应返回甜心老板 Definition");
        Assert(ReferenceEquals(CompanionData.Registry.Get("generous_guest"), CompanionData.GenerousGuestCompanion),
            "Registry.Get(generous_guest) 应返回豪爽客 Definition");
    }

    static void Test_Registry_DuplicateId_Throws()
    {
        bool threw = false;
        try
        {
            _ = new CompanionRegistry(new[]
            {
                CompanionData.SweetBossCompanion, CompanionData.SweetBossCompanion
            });
        }
        catch (ArgumentException) { threw = true; }
        Assert(threw, "重复 Id 构造 Registry 应抛 ArgumentException");
    }

    static void Test_Registry_UnknownId_Throws()
    {
        bool threw = false;
        try { _ = CompanionData.Registry.Get("not_a_companion"); }
        catch (ArgumentException) { threw = true; }
        Assert(threw, "查询不存在的 Id 应抛 ArgumentException");
    }

    /// <summary>甜心老板食客：稀有、甜味 ≥5、绑定甜心老板伙伴。</summary>
    static void Test_SweetBossCustomer_BindsCompanion_AndCondition()
    {
        var def = CustomerData.SweetBossCustomer;
        Assert(def.IsRare, "甜心老板食客应为稀有");
        Assert(def.Name == "甜心老板", "食客 Name 应为「甜心老板」");
        Assert(ReferenceEquals(def.CompanionReward, CompanionData.SweetBossCompanion),
            "甜心老板食客应绑定甜心老板伙伴");
        Assert(def.SatisfactionConditions.Count == 1, "甜心老板食客应恰有 1 个满意条件");
        var cond = def.SatisfactionConditions[0];
        Assert(cond.ConditionType == ConditionType.FlavorAtLeast, "满意条件应为 FlavorAtLeast");
        Assert(cond.Threshold == 5, "甜味阈值应为 5");
        Assert(cond.FlavorTarget == FlavorType.Sweet, "目标味道应为甜");
    }

    /// <summary>豪爽客食客：稀有、分数 ≥20、绑定豪爽客伙伴。</summary>
    static void Test_GenerousGuestCustomer_BindsCompanion_AndCondition()
    {
        var def = CustomerData.GenerousGuestCustomer;
        Assert(def.IsRare, "豪爽客食客应为稀有");
        Assert(def.Name == "豪爽客", "食客 Name 应为「豪爽客」");
        Assert(ReferenceEquals(def.CompanionReward, CompanionData.GenerousGuestCompanion),
            "豪爽客食客应绑定豪爽客伙伴");
        Assert(def.SatisfactionConditions.Count == 1, "豪爽客食客应恰有 1 个满意条件");
        var cond = def.SatisfactionConditions[0];
        Assert(cond.ConditionType == ConditionType.ScoreAtLeast, "满意条件应为 ScoreAtLeast");
        Assert(cond.Threshold == 20, "分数阈值应为 20");
    }

    /// <summary>普通食客与通用稀有食客不绑定伙伴（回退语义）。</summary>
    static void Test_NormalAndGenericRare_HaveNoCompanionReward()
    {
        Assert(CustomerData.NormalCustomer.CompanionReward == null, "普通食客不应绑定伙伴");
        Assert(CustomerData.RareCustomer.CompanionReward == null, "通用稀有食客不应绑定伙伴");
    }

    /// <summary>默认配置的稀有池包含两个具名稀有食客。</summary>
    static void Test_AppearanceConfig_DefaultRareCustomers_ContainsBothNamed()
    {
        var cfg = new CustomerAppearanceConfig();
        Assert(cfg.RareCustomers.Count == 2, $"默认稀有池应有 2 个，实际 {cfg.RareCustomers.Count}");
        Assert(cfg.RareCustomers.Contains(CustomerData.SweetBossCustomer), "默认稀有池应含甜心老板");
        Assert(cfg.RareCustomers.Contains(CustomerData.GenerousGuestCustomer), "默认稀有池应含豪爽客");
    }

    /// <summary>稀有池为空：即使命中稀有碗也回退普通食客，不产生 null。</summary>
    static void Test_AppearanceConfig_EmptyRareCustomers_FallsBackToNormal()
    {
        var cfg = new CustomerAppearanceConfig
        {
            RareBowlNumbers = new[] { 1 },
            RareProbability = 1.0,
            RareCustomers = Array.Empty<CustomerDefinition>(),
        };

        var inst = cfg.CreateCustomerForBowl(1, new Random(1));
        Assert(!inst.Definition.IsRare, "稀有池为空时应回退普通食客");
        Assert(ReferenceEquals(inst.Definition, CustomerData.NormalCustomer), "回退的应为普通食客 Definition");
    }

    /// <summary>命中稀有碗时从注入的稀有池取定义。</summary>
    static void Test_AppearanceConfig_UsesInjectedRareCustomer()
    {
        var custom = new CustomerDefinition(
            id: "custom_rare", name: "自定义稀有", isRare: true,
            satisfactionConditions: new[]
            {
                new CustomerSatisfactionCondition(ConditionType.ScoreAtLeast, 1)
            });

        var cfg = new CustomerAppearanceConfig
        {
            RareBowlNumbers = new[] { 1 },
            RareProbability = 1.0,
            RareCustomers = new[] { custom },
        };

        var inst = cfg.CreateCustomerForBowl(1, new Random(2));
        Assert(inst.Definition.IsRare, "命中稀有碗应为稀有食客");
        Assert(ReferenceEquals(inst.Definition, custom), "应使用注入的自定义稀有食客");
    }

    static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception($"[FAIL] CompanionDataTests: {message}");
    }
}
