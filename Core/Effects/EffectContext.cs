using SevenSpices.Core.Game;
using SevenSpices.Core.Ingredients;

namespace SevenSpices.Core.Effects;

/// <summary>
/// 一次完整效果链的上下文。
/// 每次食材加入锅时必须创建新的 EffectContext（含新 ChainID）。
/// 通过 TriggeredSources 防止同一触发源在同一条效果链中无限重复触发。
/// </summary>
public class EffectContext
{
    /// <summary>本次效果链的唯一标识，使用 Guid 保证全局唯一。</summary>
    public string ChainId { get; }

    /// <summary>当前正在处理的碗编号。</summary>
    public int CurrentBowl { get; }

    /// <summary>当前触发效果链的食材实例（可为 null，例如道具触发的链）。</summary>
    public IngredientInstance? CurrentIngredient { get; }

    /// <summary>本次链中已经触发过的来源 ID（触发源去重用）。</summary>
    public IReadOnlySet<string> TriggeredSources => _triggeredSources;

    /// <summary>本次链中已经执行过的效果 ID（按执行顺序记录）。</summary>
    public IReadOnlyList<string> TriggeredEffects => _triggeredEffects;

    /// <summary>当前游戏状态。</summary>
    public GameState GameState { get; }

    /// <summary>当前锅状态。</summary>
    public PotState PotState { get; }

    private readonly HashSet<string> _triggeredSources = new();
    private readonly List<string> _triggeredEffects = new();

    public EffectContext(
        int currentBowl,
        GameState gameState,
        PotState potState,
        IngredientInstance? currentIngredient = null)
    {
        ChainId = Guid.NewGuid().ToString();
        CurrentBowl = currentBowl;
        GameState = gameState;
        PotState = potState;
        CurrentIngredient = currentIngredient;
    }

    /// <summary>
    /// 尝试标记一个触发源为"本链已触发"。
    /// 返回 true 表示首次触发（可以继续执行）；false 表示已触发过（应跳过，防止循环）。
    /// </summary>
    public bool TryMarkSource(string sourceId)
    {
        return _triggeredSources.Add(sourceId);
    }

    /// <summary>记录一个效果已被执行。</summary>
    public void RecordEffect(string effectId)
    {
        _triggeredEffects.Add(effectId);
    }
}
