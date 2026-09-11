using SevenSpices.Core.Game;

namespace SevenSpices.Core.Effects;

/// <summary>
/// 固定增加指定味道值的效果。
/// </summary>
public sealed class AddFlavorEffect : IEffect
{
    public string EffectId { get; }
    public FlavorType Flavor => _flavor;
    public int Amount => _amount;
    private readonly FlavorType _flavor;
    private readonly int _amount;

    public AddFlavorEffect(FlavorType flavor, int amount, string? effectId = null)
    {
        _flavor = flavor;
        _amount = amount;
        EffectId = effectId ?? $"add_{flavor.ToString().ToLower()}_{amount}";
    }

    public void Apply(EffectContext context) =>
        context.PotState.AddFlavor(_flavor, _amount);
}
