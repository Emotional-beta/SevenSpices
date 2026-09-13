namespace SevenSpices.Core.Flavors;

/// <summary>
/// 味道动词的窄接口：一种味道对应一个动词，动词只通过 <see cref="FlavorContext"/> 改动味道。
/// 具体动词实现见 <c>SevenSpices.Core.Flavors.Verbs</c>。
/// </summary>
public interface IFlavorVerb
{
    /// <summary>在本次加料中结算一次该味道的动词。</summary>
    void Apply(FlavorContext context);
}
