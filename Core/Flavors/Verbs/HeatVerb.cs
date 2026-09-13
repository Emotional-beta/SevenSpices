using SevenSpices.Core.Game;

namespace SevenSpices.Core.Flavors.Verbs;

/// <summary>
/// 辣·余温（爆发）：本次加料使辣增加时，消耗辣值（<see cref="Content.FlavorConfig.HeatCost"/>，
/// 不足则消耗全部），使接下来若干碗的碗数倍率临时提高一档（<see cref="Content.FlavorConfig.HeatBonusTiers"/>）。
/// <para>
/// 不叠加：再次触发时取更高档位并重置剩余碗数。生效倍率由
/// <c>ScoreCalculator.GetEffectiveMultiplier</c> 读取，普通锅进入下一碗时递减剩余碗数。
/// </para>
/// </summary>
public sealed class HeatVerb : IFlavorVerb
{
    public void Apply(FlavorContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var pot = context.Pot;
        int spicy = pot.GetFlavor(FlavorType.Spicy);
        if (spicy <= 0)
            return;

        int cost = Math.Min(spicy, context.Config.HeatCost);
        pot.AddFlavor(FlavorType.Spicy, -cost);

        pot.HeatBowlsRemaining = context.Config.HeatDurationBowls;
        pot.HeatBonusTiers = Math.Max(pot.HeatBonusTiers, context.Config.HeatBonusTiers);
    }
}
