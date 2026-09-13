namespace SevenSpices.Core.Content;

/// <summary>
/// 味道系统的统一可调入口。
/// 味道动词的触发阈值、系数、封顶等数值集中在此处配置（数据驱动）。
/// 纯 C#，不依赖 Godot。
/// </summary>
public class FlavorConfig
{
    /// <summary>每种味道的默认分值权重（基础分 = Σ(味道值 × 权重)）。</summary>
    public double DefaultFlavorWeight { get; init; } = 1.0;

    /// <summary>甜·复制每次复制「一份」的数值（默认 1）。</summary>
    public int SweetDuplicateAmount { get; init; } = 1;

    // ── F3：五味（苦/辣/鲜/咸/麻）数值。以下数值是味道系统数值的唯一可调入口。 ──

    /// <summary>苦·陈酿：本次加料基础分增量存入陈酿池的比例（占位 0.5）。</summary>
    public double AgingDepositRatio { get; init; } = 0.5;

    /// <summary>苦·陈酿：此后每次加料陈酿池的复利增长率（占位 0.1）。</summary>
    public double AgingGrowthRate { get; init; } = 0.1;

    /// <summary>苦·陈酿：存入后经过多少次加料到期兑现（占位 3）。</summary>
    public int AgingMatureAdds { get; init; } = 3;

    /// <summary>辣·余温：每次触发消耗的辣值，不足则消耗全部（占位 3）。</summary>
    public int HeatCost { get; init; } = 3;

    /// <summary>辣·余温：碗数倍率加成持续的碗数（占位 2）。</summary>
    public int HeatDurationBowls { get; init; } = 2;

    /// <summary>辣·余温：碗数倍率提高的档数（占位 1）。</summary>
    public int HeatBonusTiers { get; init; } = 1;

    /// <summary>鲜·提鲜：每多一种味道，非鲜味道分的乘算系数增量（占位 0.2）。</summary>
    public double UmamiBonusPerType { get; init; } = 0.2;

    /// <summary>鲜·提鲜：非鲜味道分乘算系数的封顶（占位 3.0）。</summary>
    public double UmamiMaxMultiplier { get; init; } = 3.0;

    /// <summary>麻·共振：按最高味道「份数」再触发其动词的最大次数（占位 3）。</summary>
    public int NumbingResonanceCap { get; init; } = 3;

    // ── F4：味道熵（杂·丰盛 / 精·超频 / 寡淡）。所有数值为占位，待调。 ──

    /// <summary>杂·丰盛：激活味道种类数每多 1 种，全锅分数乘算系数的增量（占位 0.1）。</summary>
    public double AbundancePerType { get; init; } = 0.1;

    /// <summary>杂·丰盛：丰盛倍率的封顶（占位 2.0）。</summary>
    public double AbundanceMaxMultiplier { get; init; } = 2.0;

    /// <summary>寡淡：仅 1 种激活味道时全锅分数的乘算惩罚系数（占位 0.9，&lt;1 为轻量减分）。</summary>
    public double BlandPenalty { get; init; } = 0.9;

    /// <summary>
    /// 精·动词超频：同一味道每多出多少份，其动词效果增强一档（占位 5）。
    /// 依据 §11.1「每种味道的动词每次加料最多结算一次」，超频<b>不得</b>靠重复触发实现，
    /// 而是放大动词效果数值（见 PotState.GetVerbPotency）。
    /// </summary>
    public int SpecializationStep { get; init; } = 5;

    /// <summary>精·动词超频：动词效果增强的档位封顶（占位 3）。</summary>
    public int SpecializationMaxPotency { get; init; } = 3;

    /// <summary>全局默认配置，供 PotState 取默认权重与互动层取默认数值使用。</summary>
    public static FlavorConfig Default { get; } = new();
}
