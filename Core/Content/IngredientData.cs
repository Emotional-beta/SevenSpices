using SevenSpices.Core.Effects;
using SevenSpices.Core.Game;
using SevenSpices.Core.Ingredients;

namespace SevenSpices.Core.Content;

/// <summary>
/// 正式游戏食材数据工厂，提供 10 个基础食材 Definition 及其 Registry。
/// </summary>
public static class IngredientData
{
    // ── Definition 静态属性 ────────────────────────────────────────────────────

    /// <summary>米饭：基础分1，鲜+1，无条件效果。</summary>
    public static IngredientDefinition Rice { get; } = new(
        id: "rice",
        name: "米饭",
        rarity: IngredientRarity.Common,
        baseScore: 1,
        flavors: new() { [FlavorType.Umami] = 1 });

    /// <summary>糖：基础分2，甜+1，无条件效果。</summary>
    public static IngredientDefinition Sugar { get; } = new(
        id: "sugar",
        name: "糖",
        rarity: IngredientRarity.Common,
        baseScore: 2,
        flavors: new() { [FlavorType.Sweet] = 1 });

    /// <summary>辣椒：基础分2，辣+1，无条件效果。</summary>
    public static IngredientDefinition Pepper { get; } = new(
        id: "pepper",
        name: "辣椒",
        rarity: IngredientRarity.Common,
        baseScore: 2,
        flavors: new() { [FlavorType.Spicy] = 1 });

    /// <summary>红枣：基础分1，甜+1，甜≥3时额外+2分。</summary>
    public static IngredientDefinition RedDate { get; } = new(
        id: "red_date",
        name: "红枣",
        rarity: IngredientRarity.Common,
        baseScore: 1,
        flavors: new() { [FlavorType.Sweet] = 1 },
        effects: new IEffect[]
        {
            new ConditionalFlavorScoreEffect(FlavorType.Sweet, threshold: 3, bonus: 2)
        });

    /// <summary>姜：基础分1，辣+1，辣≥3时额外+2分。</summary>
    public static IngredientDefinition Ginger { get; } = new(
        id: "ginger",
        name: "姜",
        rarity: IngredientRarity.Common,
        baseScore: 1,
        flavors: new() { [FlavorType.Spicy] = 1 },
        effects: new IEffect[]
        {
            new ConditionalFlavorScoreEffect(FlavorType.Spicy, threshold: 3, bonus: 2)
        });

    /// <summary>醋：基础分1，酸+1，甜≥3时额外+2分（注意：触发条件是甜，不是酸）。</summary>
    public static IngredientDefinition Vinegar { get; } = new(
        id: "vinegar",
        name: "醋",
        rarity: IngredientRarity.Common,
        baseScore: 1,
        flavors: new() { [FlavorType.Sour] = 1 },
        effects: new IEffect[]
        {
            new ConditionalFlavorScoreEffect(FlavorType.Sweet, threshold: 3, bonus: 2)
        });

    /// <summary>蜂蜜：基础分2，甜+1，每3点甜+2分。</summary>
    public static IngredientDefinition Honey { get; } = new(
        id: "honey",
        name: "蜂蜜",
        rarity: IngredientRarity.Rare,
        baseScore: 2,
        flavors: new() { [FlavorType.Sweet] = 1 },
        effects: new IEffect[]
        {
            new ScaledFlavorScoreEffect(FlavorType.Sweet, perN: 3, bonus: 2)
        });

    /// <summary>辣油：基础分2，辣+1，每3点辣+2分。</summary>
    public static IngredientDefinition ChiliOil { get; } = new(
        id: "chili_oil",
        name: "辣油",
        rarity: IngredientRarity.Rare,
        baseScore: 2,
        flavors: new() { [FlavorType.Spicy] = 1 },
        effects: new IEffect[]
        {
            new ScaledFlavorScoreEffect(FlavorType.Spicy, perN: 3, bonus: 2)
        });

    /// <summary>冰块：基础分0，无味道，最终分数×1.5。</summary>
    public static IngredientDefinition IceCube { get; } = new(
        id: "ice_cube",
        name: "冰块",
        rarity: IngredientRarity.Rare,
        baseScore: 0,
        effects: new IEffect[]
        {
            new FinalScoreMultiplierEffect(1.5)
        });

    /// <summary>鸡蛋：基础分3，鲜+1，锅中已有3种不同食材时+3分。</summary>
    public static IngredientDefinition Egg { get; } = new(
        id: "egg",
        name: "鸡蛋",
        rarity: IngredientRarity.Common,
        baseScore: 3,
        flavors: new() { [FlavorType.Umami] = 1 },
        effects: new IEffect[]
        {
            new UniqueIngredientCountScoreEffect(requiredCount: 3, bonus: 3)
        });

    // ── Registry ───────────────────────────────────────────────────────────────

    /// <summary>包含全部 10 种正式食材的 Registry 实例。</summary>
    public static IngredientRegistry Registry { get; } = new(new[]
    {
        Rice, Sugar, Pepper, RedDate, Ginger, Vinegar, Honey, ChiliOil, IceCube, Egg
    });

    // ── 工厂方法 ──────────────────────────────────────────────────────────────

    /// <summary>从正式 Registry 按 ID 创建新的 IngredientInstance。</summary>
    public static IngredientInstance CreateInstance(string ingredientId) =>
        new(Registry.Get(ingredientId));

    /// <summary>
    /// 创建初始食材篮：5 个米饭 + 1 个职业特殊食材。
    /// 职业系统未实现，暂用 1 个辣椒占位。
    /// </summary>
    public static IReadOnlyList<IngredientInstance> CreateInitialBasket()
    {
        var basket = new List<IngredientInstance>(6);
        for (int i = 0; i < 5; i++)
            basket.Add(new IngredientInstance(Rice));

        // TODO 职业系统：用玩家职业的特殊食材替换此辣椒占位。
        basket.Add(new IngredientInstance(Pepper));

        return basket;
    }
}
