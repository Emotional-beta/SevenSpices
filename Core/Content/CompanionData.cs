using SevenSpices.Core.Companions;

namespace SevenSpices.Core.Content;

/// <summary>
/// 正式伙伴数据工厂：提供两个初始伙伴 Definition 及其 Registry。
/// 行为由 <see cref="Core.Companions.ICompanionHook"/> 的独立实现类承载，不用 lambda 堆在定义里。
/// </summary>
public static class CompanionData
{
    /// <summary>甜心老板：锅底提炼后，最高味道 +2（并列取枚举顺序最早）。</summary>
    public static CompanionDefinition SweetBossCompanion { get; } = new(
        id: "sweet_boss",
        name: "甜心老板",
        description: "锅底提炼后，最高的味道 +2（并列取枚举顺序最早）。",
        hooks: new ICompanionHook[] { new SweetBossBottomHook() });

    /// <summary>豪爽客：每个食材基础分 +1。</summary>
    public static CompanionDefinition GenerousGuestCompanion { get; } = new(
        id: "generous_guest",
        name: "豪爽客",
        description: "每个食材的基础分 +1。",
        hooks: new ICompanionHook[] { new GenerousGuestIngredientHook() });

    /// <summary>包含全部正式伙伴的 Registry。</summary>
    public static CompanionRegistry Registry { get; } = new(new[]
    {
        SweetBossCompanion, GenerousGuestCompanion
    });

    /// <summary>从正式 Registry 按 ID 创建新的 CompanionInstance。</summary>
    public static CompanionInstance CreateInstance(string companionId) =>
        new(Registry.Get(companionId));
}
