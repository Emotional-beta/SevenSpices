namespace SevenSpices.Core.Companions;

/// <summary>
/// 所有伙伴扩展点的空标记接口。
/// <para>
/// 伙伴不直接修改核心状态：只能实现本命名空间下预定义的<b>窄接口</b>，
/// 由 <see cref="CompanionSystem"/> 在固定时机调用。
/// 每个窄接口的契约以各自的 XML 注释为准，违反契约的 hook 视为 bug。
/// </para>
/// 纯 C#，不依赖 Godot / 场景节点。
/// </summary>
public interface ICompanionHook
{
}
