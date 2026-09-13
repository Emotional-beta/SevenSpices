using SevenSpices.Core.Common;
using SevenSpices.Core.Effects;
using SevenSpices.Core.Game;
using SevenSpices.Core.Ingredients;

namespace SevenSpices.Core.Content;

/// <summary>
/// 正式游戏食材数据工厂，提供 13 个基础食材 Definition 及其 Registry。
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

    /// <summary>苦瓜：基础分2，苦+3。基础食材，进随机池与商店。</summary>
    public static IngredientDefinition BitterMelon { get; } = new(
        id: "bitter_melon",
        name: "苦瓜",
        rarity: IngredientRarity.Common,
        baseScore: 2,
        flavors: new() { [FlavorType.Bitter] = 3 });

    /// <summary>腌芥菜：基础分2，咸+3。基础食材，进随机池与商店。</summary>
    public static IngredientDefinition SaltedVegetable { get; } = new(
        id: "salted_vegetable",
        name: "腌芥菜",
        rarity: IngredientRarity.Common,
        baseScore: 2,
        flavors: new() { [FlavorType.Salty] = 3 });

    /// <summary>花椒：基础分2，麻+3。基础食材，进随机池与商店；与职业专属「青花椒串」区分。</summary>
    public static IngredientDefinition SichuanPepper { get; } = new(
        id: "sichuan_pepper",
        name: "花椒",
        rarity: IngredientRarity.Common,
        baseScore: 2,
        flavors: new() { [FlavorType.Numbing] = 3 });

    // ── 职业专属食材 ──────────────────────────────────────────────────────────

    /// <summary>陈年酸笋（酸）：基础分2，酸+3。职业专属，不进随机池。</summary>
    public static IngredientDefinition PickledBamboo { get; } = new(
        id: "pickled_bamboo",
        name: "陈年酸笋",
        rarity: IngredientRarity.Common,
        baseScore: 2,
        flavors: new() { [FlavorType.Sour] = 3 });

    /// <summary>麦芽糖稀（甜）：基础分2，甜+3。职业专属，不进随机池。</summary>
    public static IngredientDefinition MaltSyrup { get; } = new(
        id: "malt_syrup",
        name: "麦芽糖稀",
        rarity: IngredientRarity.Common,
        baseScore: 2,
        flavors: new() { [FlavorType.Sweet] = 3 });

    /// <summary>苦荞茶饼（苦）：基础分2，苦+3。职业专属，不进随机池。</summary>
    public static IngredientDefinition BuckwheatTea { get; } = new(
        id: "buckwheat_tea",
        name: "苦荞茶饼",
        rarity: IngredientRarity.Common,
        baseScore: 2,
        flavors: new() { [FlavorType.Bitter] = 3 });

    /// <summary>灯笼椒（辣）：基础分2，辣+3。职业专属，不进随机池。</summary>
    public static IngredientDefinition LanternPepper { get; } = new(
        id: "lantern_pepper",
        name: "灯笼椒",
        rarity: IngredientRarity.Common,
        baseScore: 2,
        flavors: new() { [FlavorType.Spicy] = 3 });

    /// <summary>干贝瑶柱（鲜）：基础分2，鲜+3。职业专属，不进随机池。</summary>
    public static IngredientDefinition DriedScallop { get; } = new(
        id: "dried_scallop",
        name: "干贝瑶柱",
        rarity: IngredientRarity.Common,
        baseScore: 2,
        flavors: new() { [FlavorType.Umami] = 3 });

    /// <summary>岩盐结晶（咸）：基础分2，咸+3。职业专属，不进随机池。</summary>
    public static IngredientDefinition RockSalt { get; } = new(
        id: "rock_salt",
        name: "岩盐结晶",
        rarity: IngredientRarity.Common,
        baseScore: 2,
        flavors: new() { [FlavorType.Salty] = 3 });

    /// <summary>青花椒串（麻）：基础分2，麻+3。职业专属，不进随机池。</summary>
    public static IngredientDefinition GreenSichuanPepper { get; } = new(
        id: "green_sichuan_pepper",
        name: "青花椒串",
        rarity: IngredientRarity.Common,
        baseScore: 2,
        flavors: new() { [FlavorType.Numbing] = 3 });

    // ── Registry ───────────────────────────────────────────────────────────────

    /// <summary>包含全部 13 种正式食材的 Registry 实例。</summary>
    public static IngredientRegistry Registry { get; } = new(new[]
    {
        Rice, Sugar, Pepper, RedDate, Ginger, Vinegar, Honey, ChiliOil, IceCube, Egg,
        BitterMelon, SaltedVegetable, SichuanPepper
    });

    /// <summary>
    /// 职业专属食材 Registry：与正式 Registry 隔离，
    /// <see cref="CreateRandomInstance"/> / <see cref="CreateRandomInstances"/> 只读正式 Registry，
    /// 因此专属食材绝不会进入随机掉落 / 商店 / 奖励池。
    /// </summary>
    public static IngredientRegistry ProfessionRegistry { get; } = new(new[]
    {
        PickledBamboo, MaltSyrup, BuckwheatTea, LanternPepper,
        DriedScallop, RockSalt, GreenSichuanPepper
    });

    // ── 工厂方法 ──────────────────────────────────────────────────────────────

    /// <summary>从正式 Registry 按 ID 创建新的 IngredientInstance。</summary>
    public static IngredientInstance CreateInstance(string ingredientId) =>
        new(Registry.Get(ingredientId));

    /// <summary>
    /// 从正式 Registry 的全部食材中随机取一个 Definition 创建实例。
    /// 用于稀有食客满意时的随机食材掉落。
    /// <paramref name="weightSelector"/> 为可选的食材权重函数（餐饮风潮倾斜用）：
    /// 为 null 或权重全等时走原有 <see cref="Random.Next(int)"/> 路径，行为与随机数消耗逐位不变。
    /// </summary>
    public static IngredientInstance CreateRandomInstance(
        Random random,
        Func<IngredientDefinition, double>? weightSelector = null)
    {
        ArgumentNullException.ThrowIfNull(random);

        var all = Registry.GetAll();
        var definition = WeightedRandom.IsUniform(all, weightSelector)
            ? all[random.Next(all.Count)]
            : all[WeightedRandom.PickIndex(all, weightSelector!, random)];
        return new IngredientInstance(definition);
    }

    /// <summary>
    /// 从正式 Registry 的全部食材中随机抽取 <paramref name="count"/> 个<b>互不重复</b>的
    /// Definition 创建实例。用于锅结束奖励候选（设计文档 §17）。
    /// <paramref name="count"/> 超过定义总数时返回全部；<paramref name="count"/> ≤ 0 时返回空。
    /// <paramref name="weightSelector"/> 为可选权重函数：为 null 或权重全等时行为与旧实现逐位一致。
    /// </summary>
    public static IReadOnlyList<IngredientInstance> CreateRandomInstances(
        int count,
        Random random,
        Func<IngredientDefinition, double>? weightSelector = null)
    {
        ArgumentNullException.ThrowIfNull(random);

        var remaining = Registry.GetAll().ToList();
        int take = Math.Min(count, remaining.Count);
        if (take <= 0)
            return Array.Empty<IngredientInstance>();

        bool weighted = !WeightedRandom.IsUniform(remaining, weightSelector);
        var instances = new List<IngredientInstance>(take);
        for (int i = 0; i < take; i++)
        {
            int index = weighted
                ? WeightedRandom.PickIndex(remaining, weightSelector!, random)
                : random.Next(remaining.Count);
            instances.Add(new IngredientInstance(remaining[index]));
            remaining.RemoveAt(index);
        }

        return instances;
    }

    /// <summary>
    /// 创建初始食材篮：5 个米饭 + 1 个该职业专属食材。
    /// </summary>
    public static IReadOnlyList<IngredientInstance> CreateInitialBasket(ProfessionDefinition profession)
    {
        ArgumentNullException.ThrowIfNull(profession);

        var basket = new List<IngredientInstance>(6);
        for (int i = 0; i < 5; i++)
            basket.Add(new IngredientInstance(Rice));

        basket.Add(new IngredientInstance(ProfessionRegistry.Get(profession.StarterIngredientId)));

        return basket;
    }

    /// <summary>
    /// 创建初始食材篮（无职业参数）：走默认职业，兼容既有调用。
    /// </summary>
    public static IReadOnlyList<IngredientInstance> CreateInitialBasket() =>
        CreateInitialBasket(ProfessionConfig.Default);
}
