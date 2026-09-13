using SevenSpices.Core.Events;

namespace SevenSpices.Core.Effects;

/// <summary>
/// 负责执行 Effect，并通过 EffectContext 防止同一效果链中同一触发源无限重复触发。
/// <para>
/// source 去重发生在<b>触发源级别</b>，而非单效果级别：同一触发源在一条效果链内只放行一次，
/// 但其携带的全部效果都会执行；下次遇到同一触发源（含 A→B→C→A 环）整批跳过。
/// </para>
/// 每次食材加入锅时，调用方须传入新建的 EffectContext（含新 ChainId）。
/// 可选注入 <see cref="EventBus"/>：注入后每执行完一个效果会发布 EffectTriggeredEvent。
/// </summary>
public class EffectSystem
{
    private readonly EventBus? _events;

    /// <param name="events">可选事件总线；为空则不发布事件（保持既有构造调用兼容）。</param>
    public EffectSystem(EventBus? events = null)
    {
        _events = events;
    }

    /// <summary>
    /// 触发一个效果。
    /// sourceId 是触发来源的标识（例如食材 InstanceId、道具 ID 等）。
    /// 若 sourceId 在当前 EffectContext 中已触发过，则跳过（防循环）。
    /// </summary>
    public void Trigger(IEffect effect, string sourceId, EffectContext context)
    {
        ArgumentNullException.ThrowIfNull(effect);
        if (!context.TryMarkSource(sourceId)) return;
        ApplyOne(effect, sourceId, context);
    }

    /// <summary>
    /// 批量触发多个效果，每个效果使用相同的 sourceId。
    /// source 去重发生在触发源级别：同一 sourceId 在本链内只标记一次，
    /// 该 sourceId 的全部效果都执行；若该 sourceId 已被触发过，则整批跳过。
    /// </summary>
    public void TriggerAll(IEnumerable<IEffect> effects, string sourceId, EffectContext context)
    {
        ArgumentNullException.ThrowIfNull(effects);
        if (!context.TryMarkSource(sourceId)) return;   // 同一触发源在本链内只放行一次（防循环）
        foreach (var effect in effects)
        {
            ArgumentNullException.ThrowIfNull(effect);
            ApplyOne(effect, sourceId, context);        // 同一触发源的多个效果全部执行
        }
    }

    private void ApplyOne(IEffect effect, string sourceId, EffectContext context)
    {
        context.RecordEffect(effect.EffectId);
        effect.Apply(context);
        _events?.Publish(new EffectTriggeredEvent(effect.EffectId, sourceId));
    }
}
