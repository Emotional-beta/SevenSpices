using SevenSpices.Core.Game;

namespace SevenSpices.Core.Customers;

/// <summary>
/// 根据 CustomerDefinition 的满意条件和当前 PotState 判断食客是否满意。
/// 纯读取，不修改任何状态。
/// </summary>
public static class SatisfactionEvaluator
{
    /// <summary>
    /// 判断食客是否满意。满意条件之间为 OR 关系，任一满足即返回 true。
    /// 无条件时返回 false。
    /// </summary>
    public static bool IsSatisfied(CustomerDefinition definition, PotState potState)
    {
        foreach (var condition in definition.SatisfactionConditions)
        {
            if (EvaluateCondition(condition, potState))
                return true;
        }
        return false;
    }

    static bool EvaluateCondition(CustomerSatisfactionCondition condition, PotState potState)
    {
        return condition.ConditionType switch
        {
            ConditionType.ScoreAtLeast => potState.FinalScore >= condition.Threshold,
            ConditionType.FlavorAtLeast => condition.FlavorTarget.HasValue
                && potState.GetFlavor(condition.FlavorTarget.Value) >= condition.Threshold,
            ConditionType.PotTotalScoreAtLeast => potState.TotalFinalScore >= condition.Threshold,
            _ => false,
        };
    }
}
