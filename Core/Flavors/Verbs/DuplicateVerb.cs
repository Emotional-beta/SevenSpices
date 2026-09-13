using SevenSpices.Core.Game;

namespace SevenSpices.Core.Flavors.Verbs;

/// <summary>
/// 甜·复制：把锅中「值最高」的味道增加 <see cref="Content.FlavorConfig.SweetDuplicateAmount"/> 一份。
/// <para>
/// 并列最高时按 <see cref="FlavorType"/> 声明顺序取最早的一个，保证可复现。
/// 锅中味道全为 0 时不做任何写入。
/// </para>
/// </summary>
public sealed class DuplicateVerb : IFlavorVerb
{
    public void Apply(FlavorContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var pot = context.Pot;

        FlavorType highest = default;
        int highestValue = int.MinValue;

        // Enum.GetValues 按声明顺序返回；仅在严格更大时更新，故并列时保留最早的一个。
        foreach (FlavorType flavor in Enum.GetValues<FlavorType>())
        {
            int value = pot.GetFlavor(flavor);
            if (value > highestValue)
            {
                highestValue = value;
                highest = flavor;
            }
        }

        if (highestValue <= 0)
            return;

        pot.AddFlavor(highest, context.Config.SweetDuplicateAmount);
    }
}
