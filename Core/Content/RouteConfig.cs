using SevenSpices.Core.Game;

namespace SevenSpices.Core.Content;

/// <summary>
/// 路线系统（餐饮风潮）的全部数值与内容池。
/// <para>
/// <b>数值 TBD</b>：候选数量、保底收益、风潮权重系数与封顶全部集中在这里，
/// 是路线数值调优的唯一入口；不要在流程代码里分散硬编码（<c>CLAUDE.md</c> §13/§20）。
/// 纯 C#，数据驱动，不依赖 Godot。
/// </para>
/// </summary>
public static class RouteConfig
{
    // ── 候选数量（TBD） ───────────────────────────────────────────────────────

    /// <summary>每次提供路线时从保底池抽取的数量（默认 1）。</summary>
    public const int FallbackCandidateCount = 1;

    /// <summary>每次提供路线时从风潮池抽取的数量（默认 2）。</summary>
    public const int FlavorTrendCandidateCount = 2;

    // ── 风潮权重（TBD） ───────────────────────────────────────────────────────

    /// <summary>风潮主题食材的默认权重提升系数（权重 = min(1 + 系数, 封顶)）。</summary>
    public const double ThemeWeightBonus = 1.0;

    /// <summary>权重提升封顶（只倾斜不屏蔽）。</summary>
    public const double WeightCap = 3.0;

    // ── 保底收益（TBD） ───────────────────────────────────────────────────────

    /// <summary>「星君赏银」立即发放的金币数。</summary>
    public const int FallbackGold = 8;

    /// <summary>「天味饭盒」立即入篮的随机食材数量。</summary>
    public const int FallbackIngredientCount = 1;

    /// <summary>「御赐佐料」立即入道具栏的随机通用道具数量。</summary>
    public const int FallbackItemCount = 1;

    // ── 保底池 ────────────────────────────────────────────────────────────────

    /// <summary>保底池：立即到手、不改环境的稳定小收益（至少一条，抽取用）。</summary>
    public static IReadOnlyList<RouteDefinition> FallbackPool { get; } = new[]
    {
        new RouteDefinition(
            id: "xiangjun_reward_gold",
            name: "星君赏银",
            kind: RouteKind.Fallback,
            description: "监味星君：「本星君赏你几两碎银，莫声张。」",
            goldReward: FallbackGold),
        new RouteDefinition(
            id: "xiangjun_reward_ingredient",
            name: "天味饭盒",
            kind: RouteKind.Fallback,
            description: "监味星君：「天庭食堂多打的一份，拿去。」",
            ingredientRewardCount: FallbackIngredientCount),
        new RouteDefinition(
            id: "xiangjun_reward_item",
            name: "御赐佐料",
            kind: RouteKind.Fallback,
            description: "监味星君：「御膳房落下的佐料，本星君睁只眼闭只眼。」",
            itemRewardCount: FallbackItemCount),
    };

    // ── 风潮池（七味各一条） ──────────────────────────────────────────────────

    /// <summary>风潮池：按食物味道（七味）各一条，绑定主题味道与权重系数。</summary>
    public static IReadOnlyList<RouteDefinition> FlavorTrendPool { get; } = new[]
    {
        new RouteDefinition(
            id: "trend_sweet",
            name: "蜜露风",
            kind: RouteKind.FlavorTrend,
            description: "监味星君：「本星君掐指一算，下章人间好甜口。」",
            theme: FlavorType.Sweet,
            weightBonus: ThemeWeightBonus),
        new RouteDefinition(
            id: "trend_spicy",
            name: "烈火风",
            kind: RouteKind.FlavorTrend,
            description: "监味星君：「下章灶火旺，辣味当道，趁早备着。」",
            theme: FlavorType.Spicy,
            weightBonus: ThemeWeightBonus),
        new RouteDefinition(
            id: "trend_sour",
            name: "醒神风",
            kind: RouteKind.FlavorTrend,
            description: "监味星君：「下章人口寡淡，酸味最开胃。」",
            theme: FlavorType.Sour,
            weightBonus: ThemeWeightBonus),
        new RouteDefinition(
            id: "trend_bitter",
            name: "清苦风",
            kind: RouteKind.FlavorTrend,
            description: "监味星君：「良药苦口，下章苦味有它的道理。」",
            theme: FlavorType.Bitter,
            weightBonus: ThemeWeightBonus),
        new RouteDefinition(
            id: "trend_umami",
            name: "吊汤风",
            kind: RouteKind.FlavorTrend,
            description: "监味星君：「下章讲究一个鲜字，吊汤的料会更常见。」",
            theme: FlavorType.Umami,
            weightBonus: ThemeWeightBonus),
        new RouteDefinition(
            id: "trend_salty",
            name: "盐帮风",
            kind: RouteKind.FlavorTrend,
            description: "监味星君：「下章盐帮走货，咸味不缺。」",
            theme: FlavorType.Salty,
            weightBonus: ThemeWeightBonus),
        new RouteDefinition(
            id: "trend_numbing",
            name: "麻沸风",
            kind: RouteKind.FlavorTrend,
            description: "监味星君：「下章湿气重，麻味能通经络。」",
            theme: FlavorType.Numbing,
            weightBonus: ThemeWeightBonus),
    };

    // ── 索引 ──────────────────────────────────────────────────────────────────

    private static readonly Dictionary<string, RouteDefinition> _byId = BuildIndex();

    /// <summary>全部路线（保底池 + 风潮池）。</summary>
    public static IReadOnlyList<RouteDefinition> All { get; } =
        FallbackPool.Concat(FlavorTrendPool).ToList().AsReadOnly();

    /// <summary>按 Id 查询路线定义。找不到时抛 <see cref="ArgumentException"/>。</summary>
    public static RouteDefinition Get(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Route Id cannot be empty.", nameof(id));

        return _byId.TryGetValue(id, out var definition)
            ? definition
            : throw new ArgumentException($"RouteDefinition with Id '{id}' not found.", nameof(id));
    }

    /// <summary>按 Id 查询路线定义；id 为 null / 空 / 未注册时返回 false。</summary>
    public static bool TryGet(string? id, out RouteDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            definition = null!;
            return false;
        }

        return _byId.TryGetValue(id, out definition!);
    }

    private static Dictionary<string, RouteDefinition> BuildIndex()
    {
        var map = new Dictionary<string, RouteDefinition>();
        foreach (var definition in FallbackPool.Concat(FlavorTrendPool))
        {
            if (!map.TryAdd(definition.Id, definition))
                throw new ArgumentException($"Duplicate RouteDefinition Id: '{definition.Id}'.");
        }
        return map;
    }
}
