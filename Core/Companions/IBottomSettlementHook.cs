using SevenSpices.Core.Game;

namespace SevenSpices.Core.Companions;

/// <summary>
/// 扩展点 E2：锅底提炼完成后的结算修正。
/// <para>
/// 调用时机：<c>PotController.ClosePot</c> 中，先执行 <c>BottomExtractor.Extract(pot, bottom)</c>，
/// 再按伙伴获得顺序、再按各伙伴 <see cref="CompanionDefinition.Hooks"/> 顺序依次调用。
/// </para>
/// <para>
/// <b>契约</b>：可以读取 <paramref name="pot"/>；只能写入 <paramref name="bottom"/>，
/// <b>不得修改 pot</b>（锅已结束，任何回写都会破坏提炼结果与后续锅的状态来源）。
/// </para>
/// <para>
/// <b>注意</b>：传入的 <see cref="PotState"/> 在类型上是可变的，本契约仅靠约定约束
/// 「<b>只读 pot、只写 bottom</b>」；<b>没有运行时保护</b>。越界写 pot 属于 hook 实现 bug，
/// 由评审 / 测试发现，而非由调度层拦截。
/// </para>
/// </summary>
public interface IBottomSettlementHook : ICompanionHook
{
    /// <param name="pot">已提炼完毕的本锅状态（只读用途，不得修改）。</param>
    /// <param name="bottom">待写入的锅底状态（可写）。</param>
    void OnBottomSettlement(PotState pot, BottomState bottom);
}
