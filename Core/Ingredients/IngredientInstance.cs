namespace SevenSpices.Core.Ingredients;

/// <summary>
/// 食材的具体实例，代表"食材池或篮子里这一份具体的食材"。
/// 多份相同类型的食材通过独立 InstanceId 区分。
/// InstanceId 由调用方传入，或由构造函数使用 Guid 自动生成。
/// </summary>
public class IngredientInstance
{
    public string InstanceId { get; }
    public IngredientDefinition Definition { get; }

    public IngredientInstance(IngredientDefinition definition, string? instanceId = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        Definition = definition;
        InstanceId = string.IsNullOrWhiteSpace(instanceId)
            ? Guid.NewGuid().ToString()
            : instanceId;
    }
}
