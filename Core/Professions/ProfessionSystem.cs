using SevenSpices.Core.Game;

namespace SevenSpices.Core.Professions;

/// <summary>
/// 职业系统的唯一调度入口：在固定时机按顺序调用职业的三个窄接口扩展点，
/// 并把 <see cref="PotState.VerbLink"/> 上的动词联动隔离转发给互动层。
/// <para>
/// <b>防御三件套</b>（与 <c>CompanionSystem</c> 同范式）：
/// <list type="number">
/// <item><b>快照</b>：遍历前对 hook 列表 <c>ToArray()</c>，避免 hook 中途增删导致遍历异常。</item>
/// <item><b>异常隔离</b>：单个 hook 抛异常时保留当前状态 / 返回入参，不污染核心、不抛给调用方。</item>
/// <item><b>防重入</b>：本类是纯静态无状态调度，动词联动只经 <see cref="PotState.VerbLink"/> 单次转发，
/// 不会再进入主循环，天然不会自我递归。</item>
/// </list>
/// </para>
/// 纯 C#，不依赖 Godot / 场景节点。
/// </summary>
public static class ProfessionSystem
{
    /// <summary>
    /// 开局调度：依次调用每个 hook 的 <see cref="IProfessionPotStartHook"/> 与
    /// <see cref="IProfessionRuleHook"/>（同一批，均在每锅开始时执行一次）。
    /// hooks 为空时不做任何事。
    /// </summary>
    public static void ApplyPotStart(
        IReadOnlyList<IProfessionHook>? hooks, PotState pot, PlayerState player, BottomState bottom)
    {
        ArgumentNullException.ThrowIfNull(pot);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(bottom);

        if (hooks == null)
            return;

        foreach (var hook in hooks.ToArray())
        {
            if (hook is IProfessionPotStartHook startHook)
            {
                try
                {
                    startHook.ApplyPotStart(pot, player, bottom);
                }
                catch
                {
                    // 单个 hook 异常：保留当前状态、跳过该 hook，绝不污染核心流程。
                }
            }

            if (hook is IProfessionRuleHook ruleHook)
            {
                try
                {
                    ruleHook.ApplyRuleOverrides(pot);
                }
                catch
                {
                    // 规则覆盖失败：保持 PotState 默认规则参数。
                }
            }
        }
    }

    /// <summary>
    /// 从 hook 列表中挑出动词联动钩子（V1 每个职业至多一个）；没有则返回 null。
    /// </summary>
    public static IProfessionVerbHook? FindVerbHook(IReadOnlyList<IProfessionHook>? hooks)
    {
        if (hooks == null)
            return null;

        foreach (var hook in hooks)
        {
            if (hook is IProfessionVerbHook verbHook)
                return verbHook;
        }

        return null;
    }

    /// <summary>隔离转发 <see cref="IProfessionVerbHook.GetExtraTriggerAfter"/>；无联动 / 异常时返回 null。</summary>
    public static FlavorType? GetExtraTriggerAfter(PotState pot, FlavorType resolvedFlavor)
    {
        ArgumentNullException.ThrowIfNull(pot);

        var link = pot.VerbLink;
        if (link == null)
            return null;

        try
        {
            return link.GetExtraTriggerAfter(pot, resolvedFlavor);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>隔离转发 <see cref="IProfessionVerbHook.ModifyResonanceTriggers"/>；无联动 / 异常时返回入参。</summary>
    public static int ModifyResonanceTriggers(PotState pot, int triggers)
    {
        ArgumentNullException.ThrowIfNull(pot);

        var link = pot.VerbLink;
        if (link == null)
            return triggers;

        try
        {
            return link.ModifyResonanceTriggers(pot, triggers);
        }
        catch
        {
            return triggers;
        }
    }

    /// <summary>隔离转发 <see cref="IProfessionVerbHook.GetResonanceTarget"/>；无联动 / 异常时返回 null。</summary>
    public static FlavorType? GetResonanceTarget(PotState pot)
    {
        ArgumentNullException.ThrowIfNull(pot);

        var link = pot.VerbLink;
        if (link == null)
            return null;

        try
        {
            return link.GetResonanceTarget(pot);
        }
        catch
        {
            return null;
        }
    }
}
