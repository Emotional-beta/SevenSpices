namespace SevenSpices.Core.Game;

/// <summary>
/// 锅内物理状态的 ID 常量（数据驱动，设计文档 §11.5）。
/// 新增物理状态时在此追加 ID，并在 <c>FlavorStatusRules</c> 增加对应判定 / 结算数据。
/// </summary>
public static class PotStatusIds
{
    /// <summary>臭（臭豆腐）：本锅「鲜 / 苦」双阈值触发，代价是丰盛倍率失效，锅末触发「现实转移」。</summary>
    public const string Odor = "odor";

    /// <summary>臭·现实转移已在本锅执行过的标记（保证每锅最多一次，重复 ClosePot 不二次转移）。</summary>
    public const string OdorTransferred = "odor_transferred";
}
