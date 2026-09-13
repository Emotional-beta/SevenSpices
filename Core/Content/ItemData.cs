using SevenSpices.Core.Effects;
using SevenSpices.Core.Game;
using SevenSpices.Core.Items;

namespace SevenSpices.Core.Content;

/// <summary>
/// 正式游戏道具数据工厂，提供 5 种基础道具 Definition 及其 Registry。
/// 道具只改变味道或分数，不改变食材本身（设计文档 §十五）。
/// </summary>
public static class ItemData
{
    /// <summary>仙丹粉末的统一 ID（Definition id / SpecialRegistry / BossConfig 赏赐默认值共用）。</summary>
    public const string ImmortalPowderId = "immortal_powder";

    // ── Definition 静态属性 ────────────────────────────────────────────────────

    /// <summary>甜味剂：甜 +2。</summary>
    public static ItemDefinition Sweetener { get; } = new(
        id: "sweetener",
        name: "甜味剂",
        effects: new IEffect[] { new AddFlavorEffect(FlavorType.Sweet, 2) });

    /// <summary>辣椒粉：辣 +2。</summary>
    public static ItemDefinition ChiliPowder { get; } = new(
        id: "chili_powder",
        name: "辣椒粉",
        effects: new IEffect[] { new AddFlavorEffect(FlavorType.Spicy, 2) });

    /// <summary>陈醋：酸 +2。</summary>
    public static ItemDefinition AgedVinegar { get; } = new(
        id: "aged_vinegar",
        name: "陈醋",
        effects: new IEffect[] { new AddFlavorEffect(FlavorType.Sour, 2) });

    /// <summary>味精：鲜 +2。</summary>
    public static ItemDefinition Msg { get; } = new(
        id: "msg",
        name: "味精",
        effects: new IEffect[] { new AddFlavorEffect(FlavorType.Umami, 2) });

    /// <summary>食盐：本碗 +3 分。</summary>
    public static ItemDefinition Salt { get; } = new(
        id: "salt",
        name: "食盐",
        effects: new IEffect[] { new AddScoreEffect(3) });

    // ── Registry ───────────────────────────────────────────────────────────────

    /// <summary>包含全部 5 种正式道具的 Registry 实例。</summary>
    public static ItemRegistry Registry { get; } = new(new[]
    {
        Sweetener, ChiliPowder, AgedVinegar, Msg, Salt
    });

    // ── Boss 专属道具 ──────────────────────────────────────────────────────────

    /// <summary>
    /// 仙丹粉末：饕餮满意赏赐，基础分 +N、七味各 +M（数值取自 BossConfig，TBD）。
    /// 不进商店与随机掉落——只存在于 SpecialRegistry。
    /// </summary>
    public static ItemDefinition ImmortalPowder { get; } = new(
        id: ImmortalPowderId,
        name: "仙丹粉末",
        effects: new IEffect[]
        {
            new AddScoreEffect(BossConfig.Default.ImmortalPowderBaseScore),
            new AddAllFlavorsEffect(BossConfig.Default.ImmortalPowderFlavorAmount),
        });

    /// <summary>Boss 专属道具 Registry：商店 / 随机掉落只读 Registry，Boss 赏赐只读本 Registry。</summary>
    public static ItemRegistry SpecialRegistry { get; } = new(new[] { ImmortalPowder });

    // ── 工厂方法 ──────────────────────────────────────────────────────────────

    /// <summary>从正式 Registry 按 ID 创建新的 ItemInstance。</summary>
    public static ItemInstance CreateInstance(string itemId) =>
        new(Registry.Get(itemId));

    /// <summary>创建 1 个仙丹粉末实例（Boss 满意赏赐）。</summary>
    public static ItemInstance CreateImmortalPowder() =>
        CreateSpecialInstance(ImmortalPowderId);

    /// <summary>从 Boss 专属 Registry（SpecialRegistry）按 ItemDefinition Id 创建实例。Boss 赏赐用。</summary>
    public static ItemInstance CreateSpecialInstance(string itemId) =>
        new(SpecialRegistry.Get(itemId));

    /// <summary>
    /// 从正式 Registry 的全部道具中随机取一个 Definition 创建实例。
    /// 用于稀有食客满意时的随机道具掉落。
    /// </summary>
    public static ItemInstance CreateRandomInstance(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);

        var all = Registry.GetAll();
        var definition = all[random.Next(all.Count)];
        return new ItemInstance(definition);
    }

    /// <summary>
    /// 从正式 Registry 的全部道具中随机抽取 <paramref name="count"/> 个<b>互不重复</b>的
    /// Definition 创建实例。用于商店道具陈列（设计文档 §18）。
    /// <paramref name="count"/> 超过定义总数时返回全部；<paramref name="count"/> ≤ 0 时返回空。
    /// </summary>
    public static IReadOnlyList<ItemInstance> CreateRandomInstances(int count, Random random)
    {
        ArgumentNullException.ThrowIfNull(random);

        var remaining = Registry.GetAll().ToList();
        int take = Math.Min(count, remaining.Count);
        if (take <= 0)
            return Array.Empty<ItemInstance>();

        var instances = new List<ItemInstance>(take);
        for (int i = 0; i < take; i++)
        {
            int index = random.Next(remaining.Count);
            instances.Add(new ItemInstance(remaining[index]));
            remaining.RemoveAt(index);
        }

        return instances;
    }

    /// <summary>
    /// 创建开局初始道具：从正式 Registry 随机取 1 个（设计规则：开局随机给 1 个道具）。
    /// </summary>
    public static IReadOnlyList<ItemInstance> CreateInitialItems(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);
        return new[] { CreateRandomInstance(random) };
    }
}
