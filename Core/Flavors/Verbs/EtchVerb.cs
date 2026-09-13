using SevenSpices.Core.Game;

namespace SevenSpices.Core.Flavors.Verbs;

/// <summary>
/// 酸·蚀刻：把锅中「值最低且大于 0 的其他味道」的全部数值转移给酸（该味归 0）。
/// <para>
/// 并列最低时按 <see cref="FlavorType"/> 声明顺序取最早的一个，保证可复现。
/// 若不存在其他非零味道则不动作。
/// 咸·固化生效时本锅免疫削减，蚀刻直接跳过（设计文档 §11.2）。
/// </para>
/// </summary>
public sealed class EtchVerb : IFlavorVerb
{
    public void Apply(FlavorContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var pot = context.Pot;

        // 咸·固化：本锅剩余时间免疫削减 / 负面 / 物理改写。
        if (pot.IsSolidified)
            return;

        FlavorType? target = null;
        int lowest = int.MaxValue;

        // Enum.GetValues 按声明顺序返回；仅在严格更小时更新，故并列时保留最早的一个。
        foreach (FlavorType flavor in Enum.GetValues<FlavorType>())
        {
            if (flavor == context.Flavor)
                continue;

            int value = pot.GetFlavor(flavor);
            if (value <= 0)
                continue;

            if (value < lowest)
            {
                lowest = value;
                target = flavor;
            }
        }

        if (target is null)
            return;

        int transferred = pot.GetFlavor(target.Value);
        pot.AddFlavor(target.Value, -transferred);
        pot.AddFlavor(context.Flavor, transferred);
    }
}
