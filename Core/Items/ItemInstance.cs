namespace SevenSpices.Core.Items;

/// <summary>
/// 道具的具体实例，代表"玩家持有的这一个具体道具"。
/// 多个相同类型的道具通过独立 InstanceId 区分。
/// InstanceId 由调用方传入，或由构造函数使用 Guid 自动生成。
/// </summary>
public class ItemInstance
{
    public string InstanceId { get; }
    public ItemDefinition Definition { get; }

    public ItemInstance(ItemDefinition definition, string? instanceId = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        Definition = definition;
        InstanceId = string.IsNullOrWhiteSpace(instanceId)
            ? Guid.NewGuid().ToString()
            : instanceId;
    }
}
