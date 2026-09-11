using SevenSpices.Core.Game;

namespace SevenSpices.Core.Effects;

/// <summary>
/// 条件分数效果：当某种味道 >= 阈值时，增加固定 BaseScore。
/// 例如：甜 >= 3 → +2 分。
/// </summary>
public sealed class ConditionalFlavorScoreEffect : IEffect
{
    public string EffectId { get; }
    private readonly FlavorType _flavor;
    private readonly int _threshold;
    private readonly int _bonus;

    public ConditionalFlavorScoreEffect(FlavorType flavor, int threshold, int bonus, string? effectId = null)
    {
        _flavor = flavor;
        _threshold = threshold;
        _bonus = bonus;
        EffectId = effectId ?? $"cond_{flavor.ToString().ToLower()}_gte{threshold}_add{bonus}";
    }

    public void Apply(EffectContext context)
    {
        if (context.PotState.GetFlavor(_flavor) >= _threshold)
            context.PotState.BaseScore += _bonus;
    }
}
