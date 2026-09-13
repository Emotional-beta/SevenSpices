namespace SevenSpices.Core.Professions;

/// <summary>
/// 所有职业扩展点的空标记接口。
/// <para>
/// 职业不直接修改核心状态：只能实现本命名空间下预定义的<b>窄接口</b>，
/// 由 <see cref="ProfessionSystem"/> 在固定时机调用。
/// V1 只允许 <see cref="IProfessionPotStartHook"/> / <see cref="IProfessionRuleHook"/> /
/// <see cref="IProfessionVerbHook"/> 三个扩展点，新增形态属核心改动。
/// </para>
/// 纯 C#，不依赖 Godot / 场景节点。
/// </summary>
public interface IProfessionHook
{
}
