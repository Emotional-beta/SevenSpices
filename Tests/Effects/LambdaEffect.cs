using SevenSpices.Core.Effects;

namespace SevenSpices.Tests.Effects;

/// <summary>
/// 测试专用效果实现，用 lambda 表达式模拟任意效果行为。
/// 仅供单元测试内部使用。
/// </summary>
internal sealed class LambdaEffect : IEffect
{
    private readonly Action<EffectContext> _apply;

    public string EffectId { get; }

    public LambdaEffect(string effectId, Action<EffectContext> apply)
    {
        EffectId = effectId;
        _apply = apply;
    }

    public void Apply(EffectContext context) => _apply(context);
}
