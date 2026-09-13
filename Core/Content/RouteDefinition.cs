using SevenSpices.Core.Game;

namespace SevenSpices.Core.Content;

/// <summary>路线类别：保底（立即小收益）或餐饮风潮（不再给东西、只倾斜下一章获取环境）。</summary>
public enum RouteKind
{
    /// <summary>普通保底路线：立即发放确定性收益，不改变后续获取环境。</summary>
    Fallback,

    /// <summary>特殊餐饮风潮：绑定一个主题味道，倾斜下一章的主题相关内容出现概率。</summary>
    FlavorTrend,
}

/// <summary>
/// 路线的静态定义，描述「这条路线是什么」，不包含运行时状态。
/// <para>
/// 数据驱动：新增一条保底 / 风潮只需在 <see cref="RouteConfig"/> 的池子里新增定义，
/// 不要为单条路线修改核心流程（<c>CLAUDE.md</c> §20）。
/// 风潮的权重系数数值同样集中在 <see cref="RouteConfig"/>，本类只承载定义。
/// </para>
/// </summary>
public class RouteDefinition
{
    /// <summary>唯一 Id。</summary>
    public string Id { get; }

    /// <summary>显示名（短名，装腔作势风格）。</summary>
    public string Name { get; }

    /// <summary>路线类别。</summary>
    public RouteKind Kind { get; }

    /// <summary>描述文案（星君腔调）。</summary>
    public string Description { get; }

    /// <summary>风潮主题味道；<see cref="RouteKind.Fallback"/> 时为 null。</summary>
    public FlavorType? Theme { get; }

    /// <summary>风潮的主题食材权重提升系数（仅 <see cref="RouteKind.FlavorTrend"/> 有意义；TBD 由 RouteConfig 给出）。</summary>
    public double WeightBonus { get; }

    /// <summary>保底立即发放的金币（仅 <see cref="RouteKind.Fallback"/>）。</summary>
    public int GoldReward { get; }

    /// <summary>保底立即发放的随机食材入篮数量（仅 <see cref="RouteKind.Fallback"/>）。</summary>
    public int IngredientRewardCount { get; }

    /// <summary>保底立即发放的随机通用道具入道具栏数量（仅 <see cref="RouteKind.Fallback"/>）。</summary>
    public int ItemRewardCount { get; }

    public RouteDefinition(
        string id,
        string name,
        RouteKind kind,
        string description,
        FlavorType? theme = null,
        double weightBonus = 0.0,
        int goldReward = 0,
        int ingredientRewardCount = 0,
        int itemRewardCount = 0)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("RouteDefinition Id cannot be empty.", nameof(id));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("RouteDefinition Name cannot be empty.", nameof(name));
        if (kind == RouteKind.FlavorTrend && theme == null)
            throw new ArgumentException(
                $"FlavorTrend route '{id}' must declare a Theme.", nameof(theme));
        if (kind == RouteKind.Fallback && theme != null)
            throw new ArgumentException(
                $"Fallback route '{id}' must not declare a Theme (no trend state).", nameof(theme));
        if (weightBonus < 0)
            throw new ArgumentOutOfRangeException(nameof(weightBonus), "WeightBonus must be non-negative.");
        if (goldReward < 0 || ingredientRewardCount < 0 || itemRewardCount < 0)
            throw new ArgumentOutOfRangeException(nameof(goldReward), "Fallback rewards must be non-negative.");

        Id = id;
        Name = name;
        Kind = kind;
        Description = description ?? string.Empty;
        Theme = theme;
        WeightBonus = weightBonus;
        GoldReward = goldReward;
        IngredientRewardCount = ingredientRewardCount;
        ItemRewardCount = itemRewardCount;
    }
}
