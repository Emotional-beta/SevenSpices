namespace SevenSpices.Core.Effects;

/// <summary>
/// 当锅中已有 N 种不同食材（按 Definition.Id 去重）时，增加固定 BaseScore。
/// "已有"包含当前正在加入的这个食材（因为 AddIngredient 先 Add 再触发效果）。
/// </summary>
public sealed class UniqueIngredientCountScoreEffect : IEffect
{
    public string EffectId { get; }
    private readonly int _requiredCount;
    private readonly int _bonus;

    public UniqueIngredientCountScoreEffect(int requiredCount, int bonus, string? effectId = null)
    {
        if (requiredCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(requiredCount), "requiredCount must be positive.");
        _requiredCount = requiredCount;
        _bonus = bonus;
        EffectId = effectId ?? $"unique_ingr_gte{requiredCount}_add{bonus}";
    }

    public void Apply(EffectContext context)
    {
        int uniqueCount = context.PotState.Ingredients
            .Select(i => i.Definition.Id)
            .Distinct()
            .Count();

        if (uniqueCount >= _requiredCount)
            context.PotState.BaseScore += _bonus;
    }
}
