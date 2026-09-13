using SevenSpices.Core.Game;

namespace SevenSpices.Core.Effects;

/// <summary>
/// 给全部七味各增加固定数值的效果。
/// </summary>
public sealed class AddAllFlavorsEffect : IEffect
{
    public string EffectId { get; }
    public int Amount => _amount;
    private readonly int _amount;

    public AddAllFlavorsEffect(int amount, string? effectId = null)
    {
        _amount = amount;
        EffectId = effectId ?? $"add_all_flavors_{amount}";
    }

    public void Apply(EffectContext context)
    {
        foreach (FlavorType flavor in Enum.GetValues<FlavorType>())
            context.PotState.AddFlavor(flavor, _amount);
    }
}
