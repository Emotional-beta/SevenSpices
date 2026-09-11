namespace SevenSpices.Core.Customers;

/// <summary>
/// 食客的静态定义，描述"这种食客是什么"。
/// 不包含任何运行时状态，不执行满意判断逻辑。
/// </summary>
public class CustomerDefinition
{
    public string Id { get; }
    public string Name { get; }

    /// <summary>
    /// 食客的满意条件列表。多个条件之间为 OR 关系（满足任一即满意）。
    /// </summary>
    public IReadOnlyList<CustomerSatisfactionCondition> SatisfactionConditions { get; }

    public CustomerDefinition(
        string id,
        string name,
        IEnumerable<CustomerSatisfactionCondition>? satisfactionConditions = null)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("CustomerDefinition Id cannot be empty.", nameof(id));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("CustomerDefinition Name cannot be empty.", nameof(name));

        Id = id;
        Name = name;
        SatisfactionConditions = satisfactionConditions?.ToList().AsReadOnly()
            ?? new List<CustomerSatisfactionCondition>().AsReadOnly();
    }
}
