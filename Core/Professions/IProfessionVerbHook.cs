using SevenSpices.Core.Game;

namespace SevenSpices.Core.Professions;

/// <summary>
/// 职业扩展点③：动词联动。
/// <para>
/// 由 <c>FlavorInteractionSystem</c> 在味道动词结算处读取（经 <see cref="ProfessionSystem"/> 隔离调用）。
/// 职业开局时被注入到 <see cref="PotState.VerbLink"/>，每锅独立。
/// </para>
/// <para>
/// <b>契约</b>：只能读取 <paramref name="pot"/>；不得修改 Controller / GameState。
/// </para>
/// </summary>
public interface IProfessionVerbHook : IProfessionHook
{
    /// <summary>
    /// 某味道动词刚结算后，返回要额外再触发一次的味道；null 表示不触发。
    /// <b>实现方必须避免返回 <paramref name="resolvedFlavor"/> 自身</b>（防自递归）。
    /// </summary>
    FlavorType? GetExtraTriggerAfter(PotState pot, FlavorType resolvedFlavor);

    /// <summary>修改麻·共振的触发次数（默认原样返回）。</summary>
    int ModifyResonanceTriggers(PotState pot, int triggers);

    /// <summary>覆盖麻·共振的目标味道；null 表示沿用默认（当前最高味）。</summary>
    FlavorType? GetResonanceTarget(PotState pot);
}
