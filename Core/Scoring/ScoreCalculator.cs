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
    /// 根据 pot.BowlNumber、pot.BaseScore 与 pot.FinalScoreMultiplier 计算最终分数，不修改任何状态。
    /// 应用公式：Floor(BaseScore × BowlMultiplier × FinalScoreMultiplier)。
    /// 供结算与预览共用，避免两处公式漂移。
    /// </summary>
    public static int ComputeFinalScore(PotState pot)
    {
        ArgumentNullException.ThrowIfNull(pot);

        return (int)Math.Floor(pot.BaseScore * GetMultiplier(pot.BowlNumber) * pot.FinalScoreMultiplier);
    }

    /// <summary>
    /// 根据 pot.BowlNumber 和 pot.BaseScore 计算最终分数，写入 pot.FinalScore，并锁定分数。
    /// 应用公式：FinalScore = Floor(BaseScore × BowlMultiplier × FinalScoreMultiplier)。
    /// 如果分数已经锁定，抛出 InvalidOperationException。
    /// </summary>
    public static void CalculateAndLock(PotState pot)
    {
        ArgumentNullException.ThrowIfNull(pot);

        if (pot.IsScoreLocked)
            throw new InvalidOperationException("Score is already locked and cannot be calculated again.");

        pot.FinalScore = ComputeFinalScore(pot);
        pot.IsScoreLocked = true;
    }
}
