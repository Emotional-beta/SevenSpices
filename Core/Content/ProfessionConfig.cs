using SevenSpices.Core.Game;
using SevenSpices.Core.Professions;

namespace SevenSpices.Core.Content;

/// <summary>
/// 职业的全部起手规则参数与 7 条职业定义。
/// <para>
/// <b>数值 TBD</b>：所有起手数值（注入量、覆盖幅度）集中在这里，是职业数值调优的唯一入口，
/// 不要在钩子实现或流程代码里分散硬编码。纯 C#，数据驱动，不依赖 Godot。
/// </para>
/// </summary>
public static class ProfessionConfig
{
    // ── 起手规则参数（全部 TBD，待调） ─────────────────────────────────────────

    /// <summary>酸·江湖野厨：开局锅内自带的酸底数（TBD）。</summary>
    public const int SourStartSour = 3;

    /// <summary>苦·守缸人：开局陈酿池的「存款」（TBD）。</summary>
    public const double BitterStartAgingPool = 3.0;

    /// <summary>辣·码头小辣椒：开局余温剩余的碗数（TBD）。</summary>
    public const int SpicyStartHeatBowls = 2;

    /// <summary>辣·码头小辣椒：开局余温提高的倍率档数（TBD）。</summary>
    public const int SpicyStartHeatTiers = 1;

    /// <summary>鲜·御膳房清厨：丰盛判定的味道种类要求（放宽为 1）。</summary>
    public const int UmamiAbundanceRequirement = 1;

    // ── 7 条职业定义 ───────────────────────────────────────────────────────────

    /// <summary>酸·江湖野厨：开局自带酸底，可立刻起手蚀刻。</summary>
    public static ProfessionDefinition SourProfession { get; } = new(
        id: "sour",
        name: "江湖野厨",
        theme: FlavorType.Sour,
        description: "偷师百家、刀口夺味——专啃最弱的那一味来喂自己。开局锅内自带酸底。",
        starterIngredientId: "pickled_bamboo",
        starterItemId: "etching_primer",
        hooks: new IProfessionHook[] { new SourPotStartHook() });

    /// <summary>甜·糕点世家：甜动词结算后额外触发一次当前最高味（最高味为甜则跳过）。</summary>
    public static ProfessionDefinition SweetProfession { get; } = new(
        id: "sweet",
        name: "糕点世家",
        theme: FlavorType.Sweet,
        description: "江南出身，手里永远多出一份，复制的味道滚成雪球。",
        starterIngredientId: "malt_syrup",
        starterItemId: "twin_spoon",
        hooks: new IProfessionHook[] { new SweetVerbHook() });

    /// <summary>苦·守缸人：开局陈酿池已有存款。</summary>
    public static ProfessionDefinition BitterProfession { get; } = new(
        id: "bitter",
        name: "守缸人",
        theme: FlavorType.Bitter,
        description: "陈年酱坊出身，耐得住性子把分存起来，到点一次兑现。开局陈酿池已有存款。",
        starterIngredientId: "buckwheat_tea",
        starterItemId: "brine_passbook",
        hooks: new IProfessionHook[] { new BitterPotStartHook() });

    /// <summary>辣·码头小辣椒：开局即处于余温状态。</summary>
    public static ProfessionDefinition SpicyProfession { get; } = new(
        id: "spicy",
        name: "码头小辣椒",
        theme: FlavorType.Spicy,
        description: "川渝码头出身，烧起辣值把倍率拉高，越吃越上头。开局即处于余温状态。",
        starterIngredientId: "lantern_pepper",
        starterItemId: "ember_charcoal",
        hooks: new IProfessionHook[] { new SpicyPotStartHook() });

    /// <summary>鲜·御膳房清厨：丰盛判定的种类要求放宽。</summary>
    public static ProfessionDefinition UmamiProfession { get; } = new(
        id: "umami",
        name: "御膳房清厨",
        theme: FlavorType.Umami,
        description: "不抢主角、专做吊汤，把别人家的味道全放大。丰盛判定的种类要求放宽。",
        starterIngredientId: "dried_scallop",
        starterItemId: "stock_paste",
        hooks: new IProfessionHook[] { new UmamiRuleHook() });

    /// <summary>咸·盐帮硬汉：开局即固化，本锅免疫削减 / 负面 / 物理改写。</summary>
    public static ProfessionDefinition SaltyProfession { get; } = new(
        id: "salty",
        name: "盐帮硬汉",
        theme: FlavorType.Salty,
        description: "漕运盐帮出身，一盐定乾坤，这锅的味道谁都别想碰。开局即固化。",
        starterIngredientId: "rock_salt",
        starterItemId: "seal_stone",
        hooks: new IProfessionHook[] { new SaltyPotStartHook() });

    /// <summary>麻·云贵术士：共振再多响一次。</summary>
    public static ProfessionDefinition NumbingProfession { get; } = new(
        id: "numbing",
        name: "云贵术士",
        theme: FlavorType.Numbing,
        description: "点一味、通百味，让每一份味道都再响一次。",
        starterIngredientId: "green_sichuan_pepper",
        starterItemId: "echo_bowl",
        hooks: new IProfessionHook[] { new NumbingVerbHook() });

    /// <summary>全部 7 条职业，顺序固定为 酸 → 甜 → 苦 → 辣 → 鲜 → 咸 → 麻。</summary>
    public static IReadOnlyList<ProfessionDefinition> All { get; } = new[]
    {
        SourProfession, SweetProfession, BitterProfession, SpicyProfession,
        UmamiProfession, SaltyProfession, NumbingProfession
    };

    /// <summary>默认职业（未选择时兜底）：酸·江湖野厨。</summary>
    public static ProfessionDefinition Default => SourProfession;

    private static readonly Dictionary<string, ProfessionDefinition> _byId = BuildIndex();

    /// <summary>按 Id 查询职业定义。找不到时抛 <see cref="ArgumentException"/>。</summary>
    public static ProfessionDefinition Get(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Profession Id cannot be empty.", nameof(id));

        return _byId.TryGetValue(id, out var definition)
            ? definition
            : throw new ArgumentException($"ProfessionDefinition with Id '{id}' not found.", nameof(id));
    }

    /// <summary>按 Id 查询职业定义；id 为 null / 空 / 未注册时返回 false。</summary>
    public static bool TryGet(string? id, out ProfessionDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            definition = null!;
            return false;
        }

        return _byId.TryGetValue(id, out definition!);
    }

    private static Dictionary<string, ProfessionDefinition> BuildIndex()
    {
        var map = new Dictionary<string, ProfessionDefinition>();
        foreach (var definition in All)
        {
            if (!map.TryAdd(definition.Id, definition))
                throw new ArgumentException($"Duplicate ProfessionDefinition Id: '{definition.Id}'.");
        }
        return map;
    }
}
