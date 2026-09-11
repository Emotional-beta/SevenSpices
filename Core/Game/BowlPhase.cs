namespace SevenSpices.Core.Game;

/// <summary>
/// 一碗粥的完整流程阶段。枚举值从 0 起连续递增，AdvanceBowlPhase 依赖此约定。
/// </summary>
public enum BowlPhase
{
    Start = 0,
    Customer,
    ItemPhase,
    IngredientSelection,
    IngredientResolve,
    ScoreCalculation,
    ScoreLocked,
    Serving,
    Reward,
    End
}
