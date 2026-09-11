namespace SevenSpices.Core.Pot;

/// <summary>
/// PreviewIngredient 的预测结果。
/// 表示"若将该食材加入当前碗，本碗 BaseScore 会变为多少"。
/// 不包含 FinalScore（最终分数需要 ScoreCalculation 阶段锁定后才有意义）。
/// </summary>
public sealed class IngredientPreview
{
    /// <summary>加入食材后，本碗预计 BaseScore（含食材基础分和所有效果加成）。</summary>
    public int PreviewBaseScore { get; }

    public IngredientPreview(int previewBaseScore)
    {
        PreviewBaseScore = previewBaseScore;
    }
}
