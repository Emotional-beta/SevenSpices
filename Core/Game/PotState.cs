using SevenSpices.Core.Content;
using SevenSpices.Core.Ingredients;

namespace SevenSpices.Core.Game;

/// <summary>
/// 当前锅的运行状态：食材、味道、分数等。
/// 只保存数据，不执行流程逻辑。
/// </summary>
public class PotState
{
    /// <summary>当前是第几碗（1-based）。</summary>
    public int BowlNumber { get; set; } = 1;

    /// <summary>本锅总碗数上限（普通锅固定10，最终锅为 int.MaxValue）。</summary>
    public int BowlLimit { get; set; } = 10;

    /// <summary>锅内已累积的食材实例（食材进锅后持续存在直到本锅结束）。</summary>
    public List<IngredientInstance> Ingredients { get; } = new();

    /// <summary>锅内当前各味道等级。</summary>
    public Dictionary<FlavorType, int> Flavors { get; } = new();

    /// <summary>
    /// 各味道的分值权重，未设置的味道取 <see cref="FlavorConfig.DefaultFlavorWeight"/>（默认 1.0）。
    /// 由道具、伙伴等「其他效果」提升；随锅存活。
    /// </summary>
    public Dictionary<FlavorType, double> FlavorWeights { get; } = new();

    /// <summary>
    /// 本碗基础分（效果结算中累积），语义为「食材基础分 + 其他效果分」，不含味道分。
    /// 味道分为派生量（<see cref="FlavorScore"/>），避免双份状态。每碗 StartBowl 时归零。
    /// </summary>
    public int BaseScore { get; set; }

    /// <summary>本锅所有已完成碗的基础分累计（含效果加成）。StartPot 时归零，不随 StartBowl 重置。</summary>
    public int TotalBaseScore { get; set; }

    /// <summary>本碗最终分（倍率应用后锁定）。</summary>
    public int FinalScore { get; set; }

    /// <summary>本碗最终分数的额外倍率（默认1.0）。由冰块等效果写入，在 ScoreCalculator 中应用。</summary>
    public double FinalScoreMultiplier { get; set; } = 1.0;

    /// <summary>本碗分数是否已锁定。</summary>
    public bool IsScoreLocked { get; set; }

    /// <summary>当前锅的生命周期阶段。</summary>
    public PotPhase Phase { get; set; } = PotPhase.NotStarted;

    /// <summary>当前碗的流程阶段。</summary>
    public BowlPhase CurrentBowlPhase { get; set; } = BowlPhase.Start;

    /// <summary>获取指定味道的当前值，不存在则返回 0。</summary>
    public int GetFlavor(FlavorType flavor) =>
        Flavors.TryGetValue(flavor, out int v) ? v : 0;

    /// <summary>增加味道值。</summary>
    public void AddFlavor(FlavorType flavor, int amount)
    {
        if (amount == 0) return;
        Flavors[flavor] = GetFlavor(flavor) + amount;
    }

    /// <summary>获取指定味道的分值权重，未设置则返回默认权重（1.0）。</summary>
    public double GetFlavorWeight(FlavorType flavor) =>
        FlavorWeights.TryGetValue(flavor, out double weight) ? weight : FlavorConfig.Default.DefaultFlavorWeight;

    /// <summary>设置指定味道的分值权重，最低为 0。</summary>
    public void SetFlavorWeight(FlavorType flavor, double weight) =>
        FlavorWeights[flavor] = Math.Max(0.0, weight);

    /// <summary>
    /// 派生只读：味道分 = Σ(各味道值 × 该味道权重)，遍历全部 7 种味道。
    /// 味道分属于基础分的一部分，但不单独存字段，避免与 Flavors 出现双份状态。
    /// </summary>
    public double FlavorScore
    {
        get
        {
            double sum = 0.0;
            foreach (FlavorType flavor in Enum.GetValues<FlavorType>())
                sum += GetFlavor(flavor) * GetFlavorWeight(flavor);
            return sum;
        }
    }

    /// <summary>便捷只读：含味道分的合计基础分（食材基础分 + 味道分 + 其他效果分）。</summary>
    public double BaseScoreWithFlavor => BaseScore + FlavorScore;

    /// <summary>
    /// 将锅状态重置为"未开始"，用于开始新的一锅。
    /// 清空食材与味道，重置分数、碗数和阶段。
    /// </summary>
    public void Reset(int bowlLimit = 10)
    {
        BowlNumber = 1;
        BowlLimit = bowlLimit;
        Ingredients.Clear();
        Flavors.Clear();
        FlavorWeights.Clear();
        BaseScore = 0;
        FinalScore = 0;
        TotalBaseScore = 0;
        FinalScoreMultiplier = 1.0;
        IsScoreLocked = false;
        Phase = PotPhase.NotStarted;
        CurrentBowlPhase = BowlPhase.Start;
    }
}
