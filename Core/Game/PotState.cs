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

    /// <summary>本碗当前基础分（效果结算中累积）。</summary>
    public int BaseScore { get; set; }

    /// <summary>本碗最终分（倍率应用后锁定）。</summary>
    public int FinalScore { get; set; }

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
}
