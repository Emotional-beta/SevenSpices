using SevenSpices.Core.Game;

namespace SevenSpices.Core.Flavors.Verbs;

/// <summary>
/// 辣·余温（爆发）：本次加料使辣增加时，需辣值达到 <see cref="Content.FlavorConfig.HeatCost"/> 才点火，
/// 正好消耗 <see cref="Content.FlavorConfig.HeatCost"/> 点辣，使接下来若干碗的碗数倍率临时提高一档
/// （<see cref="Content.FlavorConfig.HeatBonusTiers"/>）。
/// <para>
/// 辣值不足 <see cref="Content.FlavorConfig.HeatCost"/> 时不消耗、不设置余温，让辣值自然累积到下一次加辣。
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

        // 最终锅整锅一次性结算、固定 ×32，不逐碗推进；余温加成对其无意义且永不递减，
        // 故最终锅不消耗辣值、不设置余温（设计文档 §24.2）。
        if (pot.IsFinalPot)
            return;

        // 需辣值达到 HeatCost 才点火：不足则本次不消耗、不设置余温，让辣值自然累积到下次加辣。
        int spicy = pot.GetFlavor(FlavorType.Spicy);
        if (spicy < context.Config.HeatCost)
            return;

        // 正好消耗 HeatCost 点辣（不是 min），排空门槛份量后本轮点火。
        pot.AddFlavor(FlavorType.Spicy, -context.Config.HeatCost);

        pot.HeatBowlsRemaining = context.Config.HeatDurationBowls;
        pot.HeatBonusTiers = Math.Max(pot.HeatBonusTiers, context.Config.HeatBonusTiers);
    }
}
