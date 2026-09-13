namespace SevenSpices.Core.Pot;

/// <summary>
/// PreviewIngredient 的预测结果。
/// 表示"若将该食材加入当前碗，本碗 BaseScore 与最终分数会变为多少"。
/// </summary>
public sealed class IngredientPreview
{
    /// <summary>加入食材后，本碗预计 BaseScore（含食材基础分和所有效果加成）。</summary>
    public int PreviewBaseScore { get; }

    /// <summary>加入食材后，本碗预计最终分（应用碗倍率与效果倍率后，与结算公式一致）。</summary>
    public int PreviewFinalScore { get; }

    public IngredientPreview(int previewBaseScore, int previewFinalScore)
    {
        PreviewBaseScore = previewBaseScore;
        PreviewFinalScore = previewFinalScore;
    }
}
