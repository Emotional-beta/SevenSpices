namespace SevenSpices.Core.Customers;

/// <summary>
/// 食客的具体实例，代表"本碗实际出现的这一位食客"。
/// 多次出现同一种食客时，每次出现都是独立的 CustomerInstance。
/// InstanceId 由调用方传入，或由构造函数使用 Guid 自动生成。
/// </summary>
public class CustomerInstance
{
    public string InstanceId { get; }
    public CustomerDefinition Definition { get; }

    public CustomerInstance(CustomerDefinition definition, string? instanceId = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        Definition = definition;
        InstanceId = string.IsNullOrWhiteSpace(instanceId)
            ? Guid.NewGuid().ToString()
            : instanceId;
    }
}
