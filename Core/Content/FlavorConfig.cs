namespace SevenSpices.Core.Content;

/// <summary>
/// 味道系统的统一可调入口。
/// 后续味道动词的触发阈值、系数、封顶等数值都应集中在此处配置（数据驱动）。
/// 本阶段只提供味道分值权重的默认值，不实现味道动词。
/// 纯 C#，不依赖 Godot。
/// </summary>
public class FlavorConfig
{
    /// <summary>每种味道的默认分值权重（基础分 = Σ(味道值 × 权重)）。</summary>
    public double DefaultFlavorWeight { get; init; } = 1.0;

    /// <summary>全局默认配置，供 PotState 取默认权重使用。</summary>
    public static FlavorConfig Default { get; } = new();
}
