using SevenSpices.Core.Companions;
using SevenSpices.Core.Game;

namespace SevenSpices.Core.Content;

/// <summary>
/// 「甜心老板」的 E2 行为：锅底提炼完成后，把锅底中<b>最高</b>味道 +2。
/// <para>
/// 并列最高时取枚举顺序最早的一个（<see cref="FlavorType"/> 声明顺序：
/// Sour → Sweet → Bitter → Spicy → Umami），这是确定性约定，保证可复现。
/// </para>
/// <b>契约</b>：只写 <paramref name="bottom"/>，绝不修改 <paramref name="pot"/>。
/// </summary>
public sealed class SweetBossBottomHook : IBottomSettlementHook
{
    private const int Bonus = 2;

    public void OnBottomSettlement(PotState pot, BottomState bottom)
    {
        ArgumentNullException.ThrowIfNull(bottom);

        FlavorType highest = default;
        int highestValue = int.MinValue;

        // Enum.GetValues 按声明顺序返回；仅在严格更大时更新，故并列时保留最早的一个。
        foreach (FlavorType flavor in Enum.GetValues<FlavorType>())
        {
            int value = bottom.GetFlavor(flavor);
            if (value > highestValue)
            {
                highestValue = value;
                highest = flavor;
            }
        }

        // 锅底全为 0 时没有"最高味道"，不做任何写入。
        if (highestValue <= 0)
            return;

        bottom.SetFlavor(highest, highestValue + Bonus);
    }
}
