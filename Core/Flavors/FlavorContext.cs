using SevenSpices.Core.Content;
using SevenSpices.Core.Game;
using SevenSpices.Core.Ingredients;

namespace SevenSpices.Core.Flavors;

/// <summary>
/// 一次味道动词结算的上下文。
/// 动词只能通过本对象读写锅内的味道状态（<see cref="Pot"/>），不得触达场景/UI。
/// 纯 C#，不依赖 Godot。
/// </summary>
public sealed class FlavorContext
{
    /// <summary>当前锅状态（真实状态或预览快照）。</summary>
    public PotState Pot { get; }

    /// <summary>本次加料触发动词结算的食材实例。</summary>
    public IngredientInstance Source { get; }

    /// <summary>当前正在结算的味道（决定采用哪个动词）。</summary>
    public FlavorType Flavor { get; }

    /// <summary>味道系统的可调数值配置。</summary>
    public FlavorConfig Config { get; }

    public FlavorContext(PotState pot, IngredientInstance source, FlavorType flavor, FlavorConfig config)
    {
        ArgumentNullException.ThrowIfNull(pot);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(config);

        Pot = pot;
        Source = source;
        Flavor = flavor;
        Config = config;
    }
}
