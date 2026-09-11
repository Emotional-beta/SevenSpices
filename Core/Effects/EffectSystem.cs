namespace SevenSpices.Core.Effects;

/// <summary>
/// 负责执行 Effect，并通过 EffectContext 防止同一效果链中同一触发源无限重复触发。
/// 每次食材加入锅时，调用方须传入新建的 EffectContext（含新 ChainId）。
/// </summary>
public class EffectSystem
{
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
