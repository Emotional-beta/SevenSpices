namespace SevenSpices.Core.Game;

/// <summary>
/// 当前碗/锅的食客状态。
/// Phase 1 先建立基础结构，详细食客规则在 Phase 2 实现。
/// </summary>
public class CustomerState
{
    /// <summary>当前碗的食客实例 ID（null 表示还未分配）。</summary>
    public string? CurrentCustomerInstanceId { get; set; }

    /// <summary>本锅已出现的所有食客实例 ID。</summary>
    public List<string> AppearedCustomers { get; } = new();

    /// <summary>本锅所有满意的稀有食客实例 ID（用于锅结束后伙伴候选）。</summary>
    public List<string> SatisfiedRareCustomers { get; } = new();
}
