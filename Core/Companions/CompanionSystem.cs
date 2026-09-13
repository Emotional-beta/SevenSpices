using System.Collections.ObjectModel;
using SevenSpices.Core.Game;
using SevenSpices.Core.Ingredients;

namespace SevenSpices.Core.Companions;

/// <summary>
/// 伙伴系统的唯一调度入口：在固定时机按顺序调用伙伴的窄接口扩展点。
/// <para>
/// 不保存玩家伙伴状态的副本 —— 直接复用 <c>PlayerState.Companions</c> 的<b>同一列表</b>，
/// 避免双份状态不同步。列表本身由 GameState 持有，本系统只读遍历。
/// </para>
/// <para>
/// <b>组合规则</b>：遍历顺序 = 伙伴获得顺序；同一伙伴内再按 <see cref="CompanionDefinition.Hooks"/> 顺序。
/// 加算类效果（如"每个食材 +1"）天然可交换；非加算类按上述顺序执行，
/// 因此结果对给定的伙伴列表是确定性、可复现的。
/// </para>
/// <para>
/// <b>防御三件套</b>：
/// <list type="number">
/// <item><b>快照</b>：遍历前对活动列表 <c>ToArray()</c>，避免 hook 中途增删伙伴导致遍历异常。</item>
/// <item><b>异常隔离</b>：单个 hook 抛异常时保留当前值 / 跳过该 hook，不污染核心、不抛给调用方
/// （与 <c>EventBus</c> 的订阅者隔离同范式）。</item>
/// <item><b>防重入</b>：一次 dispatch 未结束时若被重入调用，直接跳过（返回入参原值 / 不执行），
/// 防止伙伴间互相触发造成无限递归或状态半更新。</item>
/// </list>
/// </para>
/// 纯 C#，不依赖 Godot / 场景节点。
/// </summary>
public class CompanionSystem
{
    private readonly IList<CompanionInstance> _active;

    /// <summary>
    /// <see cref="Active"/> 暴露的实时只读视图：包装 <c>_active</c> 本身而非拷贝，
    /// 因此遍历它仍反映后续增删，保持「同一列表」语义。
    /// </summary>
    private readonly IReadOnlyList<CompanionInstance> _activeView;

    /// <summary>
    /// 防重入标志：true 表示当前有一次 dispatch 正在进行。
    /// 单线程核心模型下用布尔标志即可；重入调用被静默跳过。
    /// <para>
    /// <b>契约</b>：E1 / E2 共用同一标志，因此<b>同一次 dispatch 链内跨扩展点调用不受支持</b>
    /// （当前 hook 只能触达自己所属的扩展点，无法在 E1 中调用 E2）。
    /// 若未来给 hook 注入 orchestrator / CompanionSystem 以允许嵌套调度，
    /// 必须把此布尔标志改为深度计数或按扩展点分设标志，否则内层调用会被整体静默跳过。
    /// </para>
    /// </summary>
    private bool _dispatching;

    /// <param name="active">
    /// 玩家的伙伴列表（应为 <c>PlayerState.Companions</c> 的同一引用，长期存在）。
    /// </param>
    public CompanionSystem(IList<CompanionInstance> active)
    {
        ArgumentNullException.ThrowIfNull(active);
        _active = active;
        // 若传入列表本身已实现 IReadOnlyList（如 List<T>），直接复用，避免额外包装；
        // 否则用 ReadOnlyCollection 包装同一实例，提供只读视图但仍实时反映增删。
        _activeView = active as IReadOnlyList<CompanionInstance>
            ?? new ReadOnlyCollection<CompanionInstance>(active);
    }

    /// <summary>当前活动伙伴（按获得顺序），与 <c>PlayerState.Companions</c> 是同一列表。只读。</summary>
    public IReadOnlyList<CompanionInstance> Active => _activeView;

    /// <summary>
    /// 扩展点 E1 调度：按获得顺序遍历伙伴、再遍历其 Hooks，
    /// 把当前基础分依次传给每个 <see cref="IIngredientBaseScoreModifier"/>。
    /// 无伙伴时返回原值；hook 抛异常时保留该步之前的值。
    /// </summary>
    public int ModifyIngredientBaseScore(IngredientInstance ingredient, int baseScore)
    {
        ArgumentNullException.ThrowIfNull(ingredient);

        // 防重入：重入调用直接返回入参，不参与本次修正链。
        if (_dispatching)
            return baseScore;

        _dispatching = true;
        try
        {
            int value = baseScore;
            foreach (var companion in _active.ToArray())
            {
                foreach (var hook in companion.Definition.Hooks)
                {
                    if (hook is not IIngredientBaseScoreModifier modifier)
                        continue;

                    try
                    {
                        value = modifier.ModifyIngredientBaseScore(ingredient, value);
                    }
                    catch
                    {
                        // 单个伙伴 hook 异常：保留当前值，继续其余伙伴，绝不污染核心流程。
                    }
                }
            }
            return value;
        }
        finally
        {
            _dispatching = false;
        }
    }

    /// <summary>
    /// 扩展点 E2 调度：按获得顺序遍历伙伴、再遍历其 Hooks，
    /// 依次调用每个 <see cref="IBottomSettlementHook"/>。
    /// 无伙伴时不执行；hook 抛异常时跳过该 hook。
    /// </summary>
    public void ApplyBottomSettlementHooks(PotState pot, BottomState bottom)
    {
        ArgumentNullException.ThrowIfNull(pot);
        ArgumentNullException.ThrowIfNull(bottom);

        // 防重入：重入调用直接跳过，避免递归结算。
        if (_dispatching)
            return;

        _dispatching = true;
        try
        {
            foreach (var companion in _active.ToArray())
            {
                foreach (var hook in companion.Definition.Hooks)
                {
                    if (hook is not IBottomSettlementHook settlement)
                        continue;

                    try
                    {
                        settlement.OnBottomSettlement(pot, bottom);
                    }
                    catch
                    {
                        // 单个伙伴 hook 异常：跳过，不污染核心流程，也不影响其余伙伴。
                    }
                }
            }
        }
        finally
        {
            _dispatching = false;
        }
    }
}
