using SevenSpices.Core.Content;
using SevenSpices.Core.Game;

namespace SevenSpices.Core.Flavors;

/// <summary>
/// 物理状态（第一版「臭」）的判定与锅末处理（设计文档 §11.5 / 架构 §32.5）。
/// <para>
/// 物理状态一律经 <see cref="PotState.Statuses"/> 通用容器承载，本类只是当前已实现状态的
/// 数据 / 规则集合；新增物理状态只需在此追加判定，不修改核心系统。
/// 纯 C#，不依赖 Godot。
/// </para>
/// </summary>
public static class FlavorStatusRules
{
    /// <summary>
    /// §11.1 第 2 步「物理状态检查」：本锅「鲜 ≥ <see cref="FlavorConfig.OdorUmamiThreshold"/>」
    /// 且「苦 ≥ <see cref="FlavorConfig.OdorBitterThreshold"/>」时激活「臭」。
    /// <para>
    /// 幂等：容器 <see cref="PotStatusContainer.Add(string)"/> 只增不减，因此重复加料不会重复触发
    /// （每锅最多一次）。咸·固化时本锅免疫物理改写，臭不触发。
    /// </para>
    /// </summary>
    public static void CheckPhysicalStates(PotState pot, FlavorConfig config)
    {
        ArgumentNullException.ThrowIfNull(pot);
        ArgumentNullException.ThrowIfNull(config);

        // 咸·固化：本锅剩余时间免疫削减 / 负面 / 物理改写，臭不触发。
        if (pot.IsSolidified)
            return;

        if (pot.GetFlavor(FlavorType.Umami) >= config.OdorUmamiThreshold
            && pot.GetFlavor(FlavorType.Bitter) >= config.OdorBitterThreshold)
        {
            pot.Statuses.Add(PotStatusIds.Odor);
        }
    }

    /// <summary>
    /// 锅末「现实转移」：臭激活时，在锅底「值 &gt; 0」的味道中取最弱的一味
    /// （并列按 <see cref="FlavorType"/> 声明顺序取最早），把它的值全部转移给鲜（该味归 0，鲜 += 该值）。
    /// <para>
    /// <b>到位规则（占位）</b>：候选味道<b>剔除「鲜」自身</b> —— 否则锅底只有鲜时会出现
    /// 「鲜转移给自己」的无意义动作。若剔除后无候选（锅底全 0 或仅剩鲜）则不动作、不报错。
    /// </para>
    /// <para>
    /// 咸·固化时本锅免疫物理改写，臭「不做现实转移」。
    /// 该改写对锅底是永久的（锅底本就跨锅）；臭状态本身随锅（<see cref="PotState.Reset"/> 清空）。
    /// 这是锅底「单调不减」的唯一例外（设计文档 §12.1 边界规则）。
    /// </para>
    /// </summary>
    public static void ApplyOdorRealityTransfer(PotState pot, BottomState bottom)
    {
        ArgumentNullException.ThrowIfNull(pot);
        ArgumentNullException.ThrowIfNull(bottom);

        if (!pot.HasOdor)
            return;

        // 咸·固化：免疫物理改写，跳过现实转移。
        if (pot.IsSolidified)
            return;

        FlavorType? target = null;
        int lowest = int.MaxValue;

        // Enum.GetValues 按声明顺序返回；仅在严格更小时更新，故并列时保留最早的一个。
        foreach (FlavorType flavor in Enum.GetValues<FlavorType>())
        {
            if (flavor == FlavorType.Umami)
                continue; // 剔除鲜自身

            int value = bottom.GetFlavor(flavor);
            if (value <= 0)
                continue;

            if (value < lowest)
            {
                lowest = value;
                target = flavor;
            }
        }

        // 锅底全 0 或仅剩鲜：不动作、不报错。
        if (target is null)
            return;

        int transferred = bottom.GetFlavor(target.Value);
        bottom.SetFlavor(target.Value, 0);
        bottom.SetFlavor(FlavorType.Umami, bottom.GetFlavor(FlavorType.Umami) + transferred);
    }
}
