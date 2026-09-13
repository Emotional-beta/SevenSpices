using SevenSpices.Core.Content;
using SevenSpices.Core.Game;

namespace SevenSpices.Core.Flavors.Verbs;

/// <summary>
/// 鲜·提鲜（放大）：鲜 &gt; 0 时，其它味道的味道分乘以 <see cref="PotState.UmamiMultiplier"/>；
/// 系数随锅中味道种类数增长：<c>min(1 + UmamiBonusPerType × (种类数 - 1), UmamiMaxMultiplier)</c>。
/// 鲜自身的味道分不参与该乘算。
/// <para>
/// 系数是「当前锅状态」的函数，互动层每次加料后都会调用 <see cref="Recalculate"/> 刷新（幂等），
/// 使引入新味道种类或鲜归零时系数同步正确。
/// </para>
/// </summary>
public sealed class UmamiVerb : IFlavorVerb
{
    public void Apply(FlavorContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Recalculate(context.Pot, context.Config);
    }

    /// <summary>按当前锅中味道种类数重算提鲜系数；鲜为 0 时复位为 1.0。幂等。</summary>
    public static void Recalculate(PotState pot, FlavorConfig config)
    {
        ArgumentNullException.ThrowIfNull(pot);
        ArgumentNullException.ThrowIfNull(config);

        if (pot.GetFlavor(FlavorType.Umami) <= 0)
        {
            pot.UmamiMultiplier = 1.0;
            return;
        }

        int typeCount = pot.ActiveFlavorTypeCount;

        double multiplier = 1.0 + config.UmamiBonusPerType * (typeCount - 1);
        multiplier = Math.Min(multiplier, config.UmamiMaxMultiplier);
        pot.UmamiMultiplier = Math.Max(1.0, multiplier);
    }
}
