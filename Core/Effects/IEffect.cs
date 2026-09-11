namespace SevenSpices.Core.Effects;

/// <summary>
/// 所有游戏效果的统一抽象。
/// 效果负责描述"发生了什么规则变化"，不负责游戏流程。
/// </summary>
public interface IEffect
{
    /// <summary>效果的唯一标识（例如 "add_score_5"、"add_sweet_3"）。</summary>
    string EffectId { get; }

    /// <summary>
    /// 执行效果。由 EffectSystem 调用，传入当前效果链上下文。
    /// </summary>
    void Apply(EffectContext context);
}
