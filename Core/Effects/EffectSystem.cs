using SevenSpices.Core.Events;

namespace SevenSpices.Core.Effects;

/// <summary>
/// 负责执行 Effect，并通过 EffectContext 防止同一效果链中同一触发源无限重复触发。
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
        if (!context.TryMarkSource(sourceId))
            return;

        context.RecordEffect(effect.EffectId);
        effect.Apply(context);

        _events?.Publish(new EffectTriggeredEvent(effect.EffectId, sourceId));
    }

    /// <summary>
    /// 批量触发多个效果，每个效果使用相同的 sourceId。
    /// </summary>
    public void TriggerAll(IEnumerable<IEffect> effects, string sourceId, EffectContext context)
    {
        foreach (var effect in effects)
            Trigger(effect, sourceId, context);
    }
}
