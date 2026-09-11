namespace SevenSpices.Core.Effects;

/// <summary>
/// 最终分数倍率效果：将 PotState.FinalScoreMultiplier 乘以指定系数。
/// 在 ScoreCalculator.CalculateAndLock 中统一应用。
/// 例如：冰块 → FinalScoreMultiplier × 1.5。
/// </summary>
public sealed class FinalScoreMultiplierEffect : IEffect
{
    public string EffectId { get; }
    public double Multiplier => _multiplier;
    private readonly double _multiplier;

    public FinalScoreMultiplierEffect(double multiplier, string? effectId = null)
    {
        if (multiplier <= 0)
            throw new ArgumentOutOfRangeException(nameof(multiplier), "Multiplier must be positive.");
        _multiplier = multiplier;
        EffectId = effectId ?? $"final_multiplier_{multiplier:F2}";
    }

    public void Apply(EffectContext context) =>
        context.PotState.FinalScoreMultiplier *= _multiplier;
}
