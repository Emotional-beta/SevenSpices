using SevenSpices.Core.Game;
using SevenSpices.Core.Professions;

namespace SevenSpices.Core.Content;

/// <summary>「酸·江湖野厨」开局注入：锅内自带酸底。</summary>
public sealed class SourPotStartHook : IProfessionPotStartHook
{
    public void ApplyPotStart(PotState pot, PlayerState player, BottomState bottom)
    {
        pot.AddFlavor(FlavorType.Sour, ProfessionConfig.SourStartSour);
    }
}

/// <summary>「苦·守缸人」开局注入：陈酿池已有存款。</summary>
public sealed class BitterPotStartHook : IProfessionPotStartHook
{
    public void ApplyPotStart(PotState pot, PlayerState player, BottomState bottom)
    {
        pot.AgingPool = ProfessionConfig.BitterStartAgingPool;
    }
}

/// <summary>「辣·码头小辣椒」开局注入：立即进入余温状态。</summary>
public sealed class SpicyPotStartHook : IProfessionPotStartHook
{
    public void ApplyPotStart(PotState pot, PlayerState player, BottomState bottom)
    {
        pot.HeatBowlsRemaining = ProfessionConfig.SpicyStartHeatBowls;
        pot.HeatBonusTiers = ProfessionConfig.SpicyStartHeatTiers;
    }
}

/// <summary>「咸·盐帮硬汉」开局注入：本锅开局即固化。</summary>
public sealed class SaltyPotStartHook : IProfessionPotStartHook
{
    public void ApplyPotStart(PotState pot, PlayerState player, BottomState bottom)
    {
        pot.IsSolidified = true;
    }
}

/// <summary>「鲜·御膳房清厨」规则覆盖：放宽丰盛判定的味道种类要求。</summary>
public sealed class UmamiRuleHook : IProfessionRuleHook
{
    public void ApplyRuleOverrides(PotState pot)
    {
        pot.AbundanceFlavorTypeRequirement = ProfessionConfig.UmamiAbundanceRequirement;
    }
}

/// <summary>
/// 「甜·糕点世家」动词联动：甜动词结算后，对「当前最高味」额外触发一次该味动词。
/// <para>
/// 最高味为甜自身时跳过（防自递归）。V1 不改共振次数 / 目标。
/// </para>
/// </summary>
public sealed class SweetVerbHook : IProfessionVerbHook
{
    public FlavorType? GetExtraTriggerAfter(PotState pot, FlavorType resolvedFlavor)
    {
        if (resolvedFlavor != FlavorType.Sweet)
            return null;

        var highest = FindHighestFlavor(pot);
        if (highest is null || highest.Value == FlavorType.Sweet)
            return null;

        return highest;
    }

    public int ModifyResonanceTriggers(PotState pot, int triggers) => triggers;

    public FlavorType? GetResonanceTarget(PotState pot) => null;

    /// <summary>取锅中值最高的味道；并列时按 <see cref="FlavorType"/> 声明顺序取最早；全 0 时返回 null。</summary>
    private static FlavorType? FindHighestFlavor(PotState pot)
    {
        FlavorType? highest = null;
        int highestValue = int.MinValue;

        foreach (FlavorType flavor in Enum.GetValues<FlavorType>())
        {
            int value = pot.GetFlavor(flavor);
            if (value > highestValue)
            {
                highestValue = value;
                highest = flavor;
            }
        }

        return highestValue > 0 ? highest : null;
    }
}

/// <summary>
/// 「麻·云贵术士」动词联动：麻·共振再多响一次（触发次数 +1）。
/// <para>
/// V1 无「指定共振目标」的 UI，故以「次数 +1」落实「再响一次」；
/// <see cref="GetResonanceTarget"/> 保留接口但返回 null（沿用默认最高味）。
/// </para>
/// </summary>
public sealed class NumbingVerbHook : IProfessionVerbHook
{
    public FlavorType? GetExtraTriggerAfter(PotState pot, FlavorType resolvedFlavor) => null;

    public int ModifyResonanceTriggers(PotState pot, int triggers) => triggers + 1;

    public FlavorType? GetResonanceTarget(PotState pot) => null;
}
