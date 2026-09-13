using SevenSpices.Core.Ingredients;

namespace SevenSpices.Core.Companions;

/// <summary>
/// 扩展点 E1：修改单个食材的基础分。
/// <para>
/// 调用时机：<c>PotController.AddIngredient</c> 在把食材计入 <c>PotState.BaseScore</c> 之前，
/// 按伙伴获得顺序、再按各伙伴 <see cref="CompanionDefinition.Hooks"/> 顺序依次传递当前值。
/// </para>
/// <para>
/// <b>契约</b>：只能返回修改后的基础分，<b>不得修改任何其它状态</b>
/// （不得写 <c>PotState</c> / <c>GameState</c> / 食材实例等）。越界修改会污染核心流程。
/// 返回值为负或其它数值均被直接采用（调用方不做额外钳制）。
/// </para>
/// <para>
/// <b>无状态 / 幂等</b>：实现必须无状态且幂等 —— 悬停预览（<c>PreviewIngredient</c>）
/// 会在快照上多次调用本扩展点，带内部状态（计数、缓存、累加器等）的 hook 会导致预演副作用，
/// 使预览结果与真实执行不一致，或多次预览之间相互污染。
/// </para>
/// </summary>
public interface IIngredientBaseScoreModifier : ICompanionHook
{
    /// <param name="ingredient">即将入锅的食材实例（只读用途）。</param>
    /// <param name="baseScore">当前基础分（可能是前序伙伴修改后的值）。</param>
    /// <returns>修改后的基础分。</returns>
    int ModifyIngredientBaseScore(IngredientInstance ingredient, int baseScore);
}
