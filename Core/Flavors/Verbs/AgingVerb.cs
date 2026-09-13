using SevenSpices.Core.Content;
using SevenSpices.Core.Game;

namespace SevenSpices.Core.Flavors.Verbs;

/// <summary>
/// 苦·陈酿（延时）：把本次加料基础分增量的一部分存入陈酿池，此后每次加料按比例复利增长，
/// 到期按当时池值兑现（floor 后并入 <see cref="PotState.BaseScore"/> 并清空）。
/// <para>
/// 互动层在动词结算后调用 <see cref="AdvanceCycle"/> 统一推进「增长 / 到期」，
/// 保证真实结算与预览快照走同一条路径。最终锅由结算入口调用 <see cref="Realize"/> 立即全额兑现
/// （设计文档 §24.2 / 架构 §32.6）。
/// </para>
/// </summary>
public sealed class AgingVerb : IFlavorVerb
{
    public void Apply(FlavorContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var pot = context.Pot;
        if (pot.GetFlavor(FlavorType.Bitter) <= 0)
            return;

        // F4 精·超频：存入比例随苦的动词档位放大（MVP 暂定，其余动词暂不缩放）。
        double deposit = context.BaseScoreAdded * context.Config.AgingDepositRatio
            * pot.GetVerbPotency(FlavorType.Bitter);
        if (deposit > 0)
            pot.AgingPool += deposit;
    }

    /// <summary>
    /// 每次加料推进陈酿周期：对已有池（含本次新存入）按
    /// <see cref="FlavorConfig.AgingGrowthRate"/> 复利增长，累计加料次数 +1，
    /// 达到 <see cref="FlavorConfig.AgingMatureAdds"/> 时到期兑现。
    /// 池为空时不计数（存入前的加料不参与本轮计时）。
    /// </summary>
    public static void AdvanceCycle(PotState pot, FlavorConfig config)
    {
        ArgumentNullException.ThrowIfNull(pot);
        ArgumentNullException.ThrowIfNull(config);

        if (pot.AgingPool <= 0)
        {
            pot.AgingAdds = 0;
            return;
        }

        pot.AgingPool *= 1.0 + config.AgingGrowthRate;
        pot.AgingAdds++;

        if (pot.AgingAdds >= config.AgingMatureAdds)
            Realize(pot);
    }

    /// <summary>立即全额兑现陈酿池：floor(池值) 并入 BaseScore，随后清空池并复位计数。</summary>
    public static void Realize(PotState pot)
    {
        ArgumentNullException.ThrowIfNull(pot);

        if (pot.AgingPool > 0)
            pot.BaseScore += (int)Math.Floor(pot.AgingPool);

        pot.AgingPool = 0;
        pot.AgingAdds = 0;
    }
}
