using SevenSpices.Core.Game;

namespace SevenSpices.Core.Effects;

/// <summary>
/// 按味道等级分段奖励 BaseScore：每 N 点味道 +M 分。
/// 例如：每 3 点甜 +2 分（甜6 → +4，甜9 → +6）。
/// </summary>
public sealed class ScaledFlavorScoreEffect : IEffect
{
    public string EffectId { get; }
    public FlavorType Flavor => _flavor;
    public int PerN => _perN;
    public int Bonus => _bonus;
    private readonly FlavorType _flavor;
    private readonly int _perN;
    private readonly int _bonus;

    public ScaledFlavorScoreEffect(FlavorType flavor, int perN, int bonus, string? effectId = null)
    {
        if (perN <= 0)
            throw new ArgumentOutOfRangeException(nameof(perN), "perN must be positive.");
        _flavor = flavor;
        _perN = perN;
        _bonus = bonus;
        EffectId = effectId ?? $"scaled_{flavor.ToString().ToLower()}_per{perN}_add{bonus}";
    }

    public void Apply(EffectContext context)
    {
        int flavorValue = context.PotState.GetFlavor(_flavor);
        int stacks = flavorValue / _perN;
        if (stacks > 0)
            context.PotState.BaseScore += stacks * _bonus;
    }
}
