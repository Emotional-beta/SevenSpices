using SevenSpices.Core.Game;

namespace SevenSpices.Core.Customers;

/// <summary>
/// 食客满意条件的类型。
/// </summary>
public enum ConditionType
{
    /// <summary>本碗最终分数 >= Threshold。</summary>
    ScoreAtLeast,

    /// <summary>本锅累计指定味道值 >= Threshold。由 SatisfactionCondition.FlavorTarget 指定具体味道。</summary>
    FlavorAtLeast,
}

/// <summary>
/// 食客满意条件的数据定义。只描述条件，不执行判断。
/// </summary>
public class CustomerSatisfactionCondition
{
    public ConditionType ConditionType { get; }

    /// <summary>条件阈值。</summary>
    public int Threshold { get; }

    /// <summary>
    /// 当 ConditionType 为 FlavorAtLeast 时，指定目标味道类型。
    /// 其他类型时为 null。
    /// </summary>
    public FlavorType? FlavorTarget { get; }

    public CustomerSatisfactionCondition(ConditionType conditionType, int threshold, FlavorType? flavorTarget = null)
    {
        if (threshold < 0)
            throw new ArgumentOutOfRangeException(nameof(threshold), "Threshold cannot be negative.");

        ConditionType = conditionType;
        Threshold = threshold;
        FlavorTarget = flavorTarget;
    }
}
