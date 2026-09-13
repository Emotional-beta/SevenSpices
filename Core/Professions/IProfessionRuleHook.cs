using SevenSpices.Core.Game;

namespace SevenSpices.Core.Professions;

/// <summary>
/// 职业扩展点②：规则参数覆盖。
/// <para>
/// 调用时机：与 <see cref="IProfessionPotStartHook"/> 同批（<c>PotController.StartPot</c> 内），
/// 以「只读意图」覆盖 <see cref="PotState"/> 上的规则参数（如丰盛判定的种类要求）。
/// </para>
/// <para>
/// <b>契约</b>：只覆盖预定义的规则参数，不得改写流程字段 / 分数 / 阶段。
/// 纯 C#，不依赖 Godot。
/// </para>
/// </summary>
public interface IProfessionRuleHook : IProfessionHook
{
    /// <param name="pot">当前锅状态（可写规则参数）。</param>
    void ApplyRuleOverrides(PotState pot);
}
