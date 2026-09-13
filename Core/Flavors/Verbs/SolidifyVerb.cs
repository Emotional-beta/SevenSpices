using SevenSpices.Core.Game;

namespace SevenSpices.Core.Flavors.Verbs;

/// <summary>
/// 咸·固化（防守）：本次加料使咸增加时，置本锅「固化」状态。
/// <para>
/// 固化后本锅剩余时间免疫削减 / 负面 / 物理改写（如 酸·蚀刻 在固化时跳过；
/// 同一免疫判定预留给 F5 的「臭·现实转移」）。
/// </para>
/// <para>
/// TODO(F5)：脱水折算——物理状态容器与液态 / 沸腾类状态实现后，
/// 在此把对应状态折算为固定分。
/// </para>
/// </summary>
public sealed class SolidifyVerb : IFlavorVerb
{
    public void Apply(FlavorContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Pot.GetFlavor(FlavorType.Salty) <= 0)
            return;

        context.Pot.IsSolidified = true;
    }
}
