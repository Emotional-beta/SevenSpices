namespace SevenSpices.Core.Companions;

/// <summary>
/// 伙伴的具体实例，代表"玩家持有的这一位伙伴"。
/// 同一种伙伴多次获得时，每次都是独立的 CompanionInstance。
/// InstanceId 由调用方传入，或由构造函数使用 Guid 自动生成。
/// </summary>
public class CompanionInstance
{
    public string InstanceId { get; }
    public CompanionDefinition Definition { get; }

    public CompanionInstance(CompanionDefinition definition, string? instanceId = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        Definition = definition;
        InstanceId = string.IsNullOrWhiteSpace(instanceId)
            ? Guid.NewGuid().ToString()
            : instanceId;
    }
}
