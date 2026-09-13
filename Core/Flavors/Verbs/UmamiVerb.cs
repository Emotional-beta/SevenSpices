namespace SevenSpices.Core.Flavors.Verbs;

/// <summary>
/// 鲜·提鲜（放大）：鲜 &gt; 0 时，其它味道的味道分乘以
/// <c>min(1 + UmamiBonusPerType × (种类数 - 1), UmamiMaxMultiplier)</c>；鲜自身不参与该乘算。
/// <para>
/// 提鲜系数已改为 <see cref="Game.PotState.UmamiMultiplier"/> 的<b>派生只读</b>属性：由当前锅状态
/// 即时计算，任何改变味道的路径（加料 / 道具 / 锅底注入）后立即正确，无需动词刷新。
/// 因此本动词保留注册仅为「鲜拥有对应动词条目」的占位，<see cref="Apply"/> 不含任何动作。
/// </para>
/// </summary>
public sealed class UmamiVerb : IFlavorVerb
{
    public void Apply(FlavorContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        // 提鲜已改为派生（PotState.UmamiMultiplier 按当前味道状态即时计算），无需动词刷新。
    }
}
