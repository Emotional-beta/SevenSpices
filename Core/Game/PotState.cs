using SevenSpices.Core.Content;
using SevenSpices.Core.Ingredients;
using SevenSpices.Core.Professions;

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

    /// <summary>
    /// 本锅是否为最终锅。最终锅整锅一次性结算（固定 ×32 档位），辣·余温对其不生效。
    /// 由 PotController.StartPot 依据 RunState 注入，<see cref="Reset"/> 复位为 false。
    /// </summary>
    public bool IsFinalPot { get; set; }

    /// <summary>
    /// 本锅生效的味道系统可调数值配置（开锅时由 PotController / RunController 注入，默认
    /// <see cref="FlavorConfig.Default"/>）。挂在 PotState 上使结算与预览读取同一份配置，
    /// 避免公式漂移；已纳入快照（PotStateSnapshot）与 <see cref="Reset"/>。
    /// </summary>
    public FlavorConfig Config { get; set; } = FlavorConfig.Default;

    /// <summary>
    /// F4 杂·丰盛倍率的味道种类门槛：激活味道种类数 &gt;= 本值时才计算丰盛倍率。
    /// 默认取自 <see cref="FlavorConfig.Default"/> 的 <see cref="FlavorConfig.AbundanceFlavorTypeRequirement"/>（2）；
    /// 可由职业规则钩子（如「鲜·御膳房清厨」）覆盖，<see cref="Reset"/> 从 <see cref="Config"/> 复位。
    /// </summary>
    public int AbundanceFlavorTypeRequirement { get; set; } =
        FlavorConfig.Default.AbundanceFlavorTypeRequirement;

    /// <summary>
    /// 本锅生效的职业动词联动钩子（开锅时由 PotController 从职业 hooks 中挑出注入，
    /// <see cref="Reset"/> 清空）。为 null 时互动层行为与无职业完全一致。
    /// </summary>
    public IProfessionVerbHook? VerbLink { get; set; }

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

    /// <summary>
    /// 本锅累计最终分：Σ 每碗锁定后的 <see cref="FinalScore"/>（含倍率）。
    /// 饕餮（Boss）判定读数；不用于预览，因此刻意不纳入 PotStateSnapshot（见方案 §4.2）。
    /// 由 ScoreCalculator.CalculateAndLock 在每次锁分后累加，StartPot/Reset 时归零。
    /// </summary>
    public int TotalFinalScore { get; set; }

    /// <summary>本碗最终分（倍率应用后锁定）。</summary>
    public int FinalScore { get; set; }

    /// <summary>本碗最终分数的额外倍率（默认1.0）。由冰块等效果写入，在 ScoreCalculator 中应用。</summary>
    public double FinalScoreMultiplier { get; set; } = 1.0;

    /// <summary>本碗分数是否已锁定。</summary>
    public bool IsScoreLocked { get; set; }

    /// <summary>
    /// 苦·陈酿：尚未兑现的陈酿池（跨碗存活、随锅重置）。到期或最终锅结算时按 floor 值
    /// 并入 <see cref="BaseScore"/> 并清空。设计文档 §11.2 / 架构 §32.4。
    /// </summary>
    public double AgingPool { get; set; }

    /// <summary>苦·陈酿：自本次存入起累计的加料次数，达到配置到期次数后兑现。</summary>
    public int AgingAdds { get; set; }

    /// <summary>咸·固化：本锅剩余时间内是否免疫削减 / 负面 / 物理改写（设计文档 §11.2）。</summary>
    public bool IsSolidified { get; set; }

    /// <summary>
    /// 锅内物理状态的通用容器（设计文档 §11.5 / 架构 §32.5）。
    /// 随锅存活、随 <see cref="Reset"/> 清空、纳入 <c>PotStateSnapshot</c> 深拷贝。
    /// 不为单个物理状态写死字段，新增状态只需加 ID 与判定数据。
    /// </summary>
    public PotStatusContainer Statuses { get; } = new();

    /// <summary>臭是否已在本锅激活（物理状态容器的等价读取）。</summary>
    public bool HasOdor => Statuses.Has(PotStatusIds.Odor);

    /// <summary>辣·余温：碗数倍率加成还剩多少碗（&gt;0 时生效，普通锅进入新碗时递减）。</summary>
    public int HeatBowlsRemaining { get; set; }

    /// <summary>辣·余温：碗数倍率提高的档数（与 <see cref="HeatBowlsRemaining"/> 配合）。</summary>
    public int HeatBonusTiers { get; set; }

    /// <summary>
    /// 派生只读：鲜·提鲜作用于非鲜味道分的乘算系数。
    /// 鲜 &gt; 0 时为 <c>min(1 + Config.UmamiBonusPerType × (ActiveFlavorTypeCount - 1), Config.UmamiMaxMultiplier)</c>，
    /// 否则为 1.0。按当前锅状态即时计算、不存字段、无需任何路径刷新，
    /// 因此加料 / 道具 / 锅底注入后立即正确，预览与结算不会漂移。
    /// </summary>
    public double UmamiMultiplier
    {
        get
        {
            if (GetFlavor(FlavorType.Umami) <= 0)
                return 1.0;

            int typeCount = ActiveFlavorTypeCount;
            double multiplier = 1.0 + Config.UmamiBonusPerType * (typeCount - 1);
            return Math.Min(multiplier, Config.UmamiMaxMultiplier);
        }
    }

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

    /// <summary>
    /// 获取指定味道的分值权重，未设置则返回本锅注入配置的默认权重
    /// （<see cref="Config"/> 的 <see cref="FlavorConfig.DefaultFlavorWeight"/>，默认 1.0）。
    /// </summary>
    public double GetFlavorWeight(FlavorType flavor) =>
        FlavorWeights.TryGetValue(flavor, out double weight) ? weight : Config.DefaultFlavorWeight;

    /// <summary>设置指定味道的分值权重，最低为 0。</summary>
    public void SetFlavorWeight(FlavorType flavor, double weight) =>
        FlavorWeights[flavor] = Math.Max(0.0, weight);

    /// <summary>
    /// 派生只读：味道分 = 鲜的味道分 + （其余味道分之和）× <see cref="UmamiMultiplier"/>。
    /// 遍历全部 7 种味道；鲜自身不参与提鲜乘算。味道分属于基础分的一部分，
    /// 但不单独存字段，避免与 Flavors 出现双份状态。
    /// </summary>
    public double FlavorScore
    {
        get
        {
            double umami = 0.0;
            double others = 0.0;
            foreach (FlavorType flavor in Enum.GetValues<FlavorType>())
            {
                double value = GetFlavor(flavor) * GetFlavorWeight(flavor);
                if (flavor == FlavorType.Umami)
                    umami += value;
                else
                    others += value;
            }
            return umami + others * UmamiMultiplier;
        }
    }

    /// <summary>便捷只读：含味道分的合计基础分（食材基础分 + 味道分 + 其他效果分）。</summary>
    public double BaseScoreWithFlavor => BaseScore + FlavorScore;

    /// <summary>
    /// 派生只读：当前「激活」的味道种类数（值 &gt; 0 的味道个数）。
    /// 是 F4 丰盛倍率与寡淡惩罚的唯一判据。
    /// </summary>
    public int ActiveFlavorTypeCount
    {
        get
        {
            int count = 0;
            foreach (FlavorType flavor in Enum.GetValues<FlavorType>())
            {
                if (GetFlavor(flavor) > 0)
                    count++;
            }
            return count;
        }
    }

    /// <summary>
    /// 精·动词超频：同一味道每满 <see cref="FlavorConfig.SpecializationStep"/> 份，其动词效果增强一档。
    /// 返回 <c>min(1 + (值-1)/步长, SpecializationMaxPotency)</c>（整数除法）；值 ≤ 1 或步长非法时为 1。
    /// <para>
    /// MVP 仅甜·复制（增量 ×potency）与苦·陈酿（存入比例 ×potency）两个「有明确数值幅度」的动词生效；
    /// 酸 / 鲜 / 咸 / 麻 暂不缩放，留待后续。
    /// </para>
    /// </summary>
    public int GetVerbPotency(FlavorType flavor)
    {
        int value = GetFlavor(flavor);
        if (value <= 1 || Config.SpecializationStep <= 0)
            return 1;

        int potency = 1 + (value - 1) / Config.SpecializationStep;
        return Math.Min(potency, Config.SpecializationMaxPotency);
    }

    /// <summary>
    /// 将锅状态重置为"未开始"，用于开始新的一锅。
    /// 清空食材与味道，重置分数、碗数和阶段。
    /// </summary>
    public void Reset(int bowlLimit = 10)
    {
        BowlNumber = 1;
        BowlLimit = bowlLimit;
        IsFinalPot = false;
        Config = FlavorConfig.Default;
        AbundanceFlavorTypeRequirement = Config.AbundanceFlavorTypeRequirement;
        VerbLink = null;
        Ingredients.Clear();
        Flavors.Clear();
        FlavorWeights.Clear();
        BaseScore = 0;
        FinalScore = 0;
        TotalBaseScore = 0;
        TotalFinalScore = 0;
        FinalScoreMultiplier = 1.0;
        IsScoreLocked = false;
        AgingPool = 0.0;
        AgingAdds = 0;
        IsSolidified = false;
        Statuses.Clear();
        HeatBowlsRemaining = 0;
        HeatBonusTiers = 0;
        Phase = PotPhase.NotStarted;
        CurrentBowlPhase = BowlPhase.Start;
    }
}
