using SevenSpices.Core.Game;

namespace SevenSpices.Core.Scoring;

/// <summary>
/// 集中处理分数计算：根据碗数获取倍率、将 BaseScore 乘以倍率写入 FinalScore 并锁定。
/// </summary>
public static class ScoreCalculator
{
    /// <summary>
    /// 最终锅固定使用的碗数倍率档位。
    /// 最终锅不逐碗结算，整锅作为「一大碗粥」一次性结算，因此固定取十碗倍率表的最高档 ×32。
    /// </summary>
    public const int FinalPotBowlNumber = 10;

    /// <summary>
    /// 根据碗数返回对应倍率。
    /// BowlNumber 1~5 → ×1，6 → ×2，7 → ×4，8 → ×8，9 → ×16，10+ → ×32。
    /// BowlNumber &lt;= 0 视为非法状态，抛出 ArgumentOutOfRangeException。
    /// </summary>
    public static int GetMultiplier(int bowlNumber)
    {
        if (bowlNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(bowlNumber), "BowlNumber must be positive.");

        return bowlNumber switch
        {
            <= 5 => 1,
            6 => 2,
            7 => 4,
            8 => 8,
            9 => 16,
            _ => 32,
        };
    }

    /// <summary>
    /// 计算实际生效的碗数倍率：碗数基础倍率 + 辣·余温的临时档位。
    /// <see cref="PotState.HeatBowlsRemaining"/> &gt; 0 时叠加 <see cref="PotState.HeatBonusTiers"/> 档。
    /// <para>
    /// 最终锅整锅一次性结算、固定取十碗倍率表的 ×32，不逐碗推进，余温永不递减，
    /// 因此 <see cref="PotState.IsFinalPot"/> 时忽略余温，只用基础倍率（设计文档 §24.2）。
    /// </para>
    /// </summary>
    public static int GetEffectiveMultiplier(PotState pot)
    {
        ArgumentNullException.ThrowIfNull(pot);

        if (pot.IsFinalPot)
            return GetMultiplier(pot.BowlNumber);

        int bonus = pot.HeatBowlsRemaining > 0 ? pot.HeatBonusTiers : 0;
        return GetMultiplier(pot.BowlNumber) + bonus;
    }

    /// <summary>
    /// 根据 pot.BowlNumber、pot.BaseScore、pot.FlavorScore、F4 味道熵（丰盛倍率 / 寡淡惩罚）
    /// 与 pot.FinalScoreMultiplier 计算最终分数，不修改任何状态。
    /// 应用公式：<c>Floor((BaseScore + FlavorScore) × 丰盛倍率 × 寡淡系数 × 生效碗数倍率 × FinalScoreMultiplier)</c>。
    /// 供结算与预览共用，避免两处公式漂移。
    /// </summary>
    public static int ComputeFinalScore(PotState pot)
    {
        ArgumentNullException.ThrowIfNull(pot);

        return (int)Math.Floor(
            pot.BaseScoreWithFlavor
            * GetAbundanceMultiplier(pot)
            * GetBlandPenalty(pot)
            * GetEffectiveMultiplier(pot)
            * pot.FinalScoreMultiplier);
    }

    /// <summary>
    /// 杂·丰盛倍率（F4）：按当前激活味道种类数计算全锅分数乘算系数。
    /// <c>种类数 &gt;= 2 ? min(1 + AbundancePerType × (种类数-1), AbundanceMaxMultiplier) : 1</c>。
    /// 0 / 1 种味道时均为 1（无加成）。
    /// </summary>
    public static double GetAbundanceMultiplier(PotState pot)
    {
        ArgumentNullException.ThrowIfNull(pot);

        // F5：臭激活时本锅味道熵奖励失效，丰盛倍率不生效（设计文档 §11.5）。
        if (pot.HasOdor)
            return 1.0;

        int typeCount = pot.ActiveFlavorTypeCount;
        if (typeCount < 2)
            return 1.0;

        var config = pot.Config;
        double multiplier = 1.0 + config.AbundancePerType * (typeCount - 1);
        return Math.Min(multiplier, config.AbundanceMaxMultiplier);
    }

    /// <summary>
    /// 寡淡惩罚（F4）：仅 1 种激活味道时返回 FlavorConfig.BlandPenalty（&lt;1 轻量减分），
    /// 其余情况（0 或 &gt;= 2 种）返回 1.0。惩罚只乘算、不阻断通关（设计文档 §11.4）。
    /// </summary>
    public static double GetBlandPenalty(PotState pot)
    {
        ArgumentNullException.ThrowIfNull(pot);
        return pot.ActiveFlavorTypeCount == 1 ? pot.Config.BlandPenalty : 1.0;
    }

    /// <summary>
    /// 根据 pot.BowlNumber、pot.BaseScore 与 pot.FlavorScore 计算最终分数，写入 pot.FinalScore，并锁定分数。
    /// 应用公式：FinalScore = Floor((BaseScore + FlavorScore) × 丰盛倍率 × 寡淡系数 × 生效碗数倍率 × FinalScoreMultiplier)。
    /// 如果分数已经锁定，抛出 InvalidOperationException。
    /// </summary>
    public static void CalculateAndLock(PotState pot)
    {
        ArgumentNullException.ThrowIfNull(pot);

        if (pot.IsScoreLocked)
            throw new InvalidOperationException("Score is already locked and cannot be calculated again.");

        pot.FinalScore = ComputeFinalScore(pot);
        pot.IsScoreLocked = true;

        // 本锅累计最终分（含倍率）：普通锅逐碗累加，最终锅一次性结算累加一次。
        pot.TotalFinalScore += pot.FinalScore;
    }
}
