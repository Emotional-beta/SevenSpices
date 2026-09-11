namespace SevenSpices.Core.Effects;

/// <summary>
/// 固定增加 BaseScore 的效果。
/// </summary>
public sealed class AddScoreEffect : IEffect
{
    public string EffectId { get; }
    public int Amount => _amount;
    private readonly int _amount;

    public AddScoreEffect(int amount, string? effectId = null)
    {
        _amount = amount;
        EffectId = effectId ?? $"add_score_{amount}";
    }

    public void Apply(EffectContext context) =>
        context.PotState.BaseScore += _amount;
}
