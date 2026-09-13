using SevenSpices.Core.Game;

namespace SevenSpices.Core.Professions;

/// <summary>
/// 职业扩展点①：开局注入。
/// <para>
/// 调用时机：<c>PotController.StartPot</c> 中，<c>Bottom.ApplyToPot(pot)</c> 之后，每锅开始时执行一次。
/// </para>
/// <para>
/// <b>契约</b>：允许写 <paramref name="pot"/> / <paramref name="player"/> / <paramref name="bottom"/>；
/// 只做一次性开局状态注入，不得持有 Controller / GameState。
/// </para>
/// </summary>
public interface IProfessionPotStartHook : IProfessionHook
{
    /// <param name="pot">刚开锅、已注入锅底的锅状态（可写）。</param>
    /// <param name="player">玩家长期资源状态（可写）。</param>
    /// <param name="bottom">当前锅底状态（可写）。</param>
    void ApplyPotStart(PotState pot, PlayerState player, BottomState bottom);
}
