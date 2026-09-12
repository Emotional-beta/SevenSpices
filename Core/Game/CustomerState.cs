using SevenSpices.Core.Customers;

namespace SevenSpices.Core.Game;

/// <summary>
/// 当前碗/锅的食客状态。
/// </summary>
public class CustomerState
{
    /// <summary>当前碗的食客实例（null 表示还未分配）。</summary>
    public CustomerInstance? CurrentCustomer { get; set; }

    /// <summary>本锅已出现的所有食客实例。</summary>
    public List<CustomerInstance> AppearedCustomers { get; } = new();

    /// <summary>本锅所有满意的稀有食客实例（用于锅结束后伙伴候选）。</summary>
    public List<CustomerInstance> SatisfiedRareCustomers { get; } = new();

    /// <summary>
    /// 重置为「本锅」初始状态：清空当前食客、本锅出现列表与满意稀有列表。
    /// 应由新一锅开始时调用（而非上一锅结束时），
    /// 以保证锅结束后到下一锅开始前，本锅 SatisfiedRareCustomers 数据仍可读。
    /// </summary>
    public void ResetForPot()
    {
        CurrentCustomer = null;
        AppearedCustomers.Clear();
        SatisfiedRareCustomers.Clear();
    }
}
