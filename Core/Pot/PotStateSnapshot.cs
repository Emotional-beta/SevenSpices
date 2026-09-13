using SevenSpices.Core.Game;
using SevenSpices.Core.Ingredients;

namespace SevenSpices.Core.Pot;

/// <summary>
/// PotState 的轻量快照，用于 PreviewIngredient 的无副作用模拟计算。
/// 继承 PotState 以便直接传入 EffectContext 和 ApplyIngredientTo，
/// 确保与真实执行路径完全相同。
/// </summary>
internal sealed class PotStateSnapshot : PotState
{
    /// <summary>
    /// 从真实 PotState 创建快照：深拷贝计算相关字段，忽略流程状态字段。
    /// </summary>
    public static PotStateSnapshot From(PotState source)
    {
        var snap = new PotStateSnapshot();
        snap.BowlNumber = source.BowlNumber;
        snap.BowlLimit = source.BowlLimit;
        snap.IsFinalPot = source.IsFinalPot;
        snap.BaseScore = source.BaseScore;
        snap.FinalScore = source.FinalScore;
        snap.FinalScoreMultiplier = source.FinalScoreMultiplier;
        snap.IsScoreLocked = source.IsScoreLocked;
        snap.Phase = source.Phase;
        snap.CurrentBowlPhase = source.CurrentBowlPhase;
        snap.TotalBaseScore = source.TotalBaseScore;
        snap.Config = source.Config;
        snap.AbundanceFlavorTypeRequirement = source.AbundanceFlavorTypeRequirement;
        snap.VerbLink = source.VerbLink;

        // F3 跨碗状态：必须深拷贝，否则悬停预测会与真实结算不一致（架构 §32.4）。
        snap.AgingPool = source.AgingPool;
        snap.AgingAdds = source.AgingAdds;
        snap.IsSolidified = source.IsSolidified;
        snap.HeatBowlsRemaining = source.HeatBowlsRemaining;
        snap.HeatBonusTiers = source.HeatBonusTiers;
        // 提鲜系数为派生只读（随 Config / Flavors 即时计算），无需拷贝。

        // F5 物理状态容器：深拷贝，否则预览得到的臭 / 丰盛失效会与真实结算不一致。
        foreach (var kv in source.Statuses.States)
            snap.Statuses.Set(kv.Key, kv.Value);

        foreach (var kv in source.Flavors)
            snap.Flavors[kv.Key] = kv.Value;

        foreach (var kv in source.FlavorWeights)
            snap.FlavorWeights[kv.Key] = kv.Value;

        foreach (var ing in source.Ingredients)
            snap.Ingredients.Add(ing);

        return snap;
    }
}
