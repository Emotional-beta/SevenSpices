using SevenSpices.Core.Companions;
using SevenSpices.Core.Ingredients;

namespace SevenSpices.Core.Content;

/// <summary>
/// 「豪爽客」的 E1 行为：每个食材基础分 +1。
/// <b>契约</b>：只返回修改后的基础分，不修改任何其它状态。
/// </summary>
public sealed class GenerousGuestIngredientHook : IIngredientBaseScoreModifier
{
    private const int Bonus = 1;

    public int ModifyIngredientBaseScore(IngredientInstance ingredient, int baseScore) =>
        baseScore + Bonus;
}
