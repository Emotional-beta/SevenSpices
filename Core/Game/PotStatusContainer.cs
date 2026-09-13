namespace SevenSpices.Core.Game;

/// <summary>
/// 锅内物理状态的通用数据驱动容器（设计文档 §11.5 / 架构 §32.5）。
/// 以「状态 ID → 强度值」承载任意物理状态（臭、糊、冻……），新增物理状态只需增加 ID 常量
/// 与判定数据，不修改核心系统。
/// <para>
/// 随锅存活、随 <see cref="PotState.Reset"/> 清空、纳入 <c>PotStateSnapshot</c> 深拷贝。
/// 纯 C#，不依赖 Godot。
/// </para>
/// </summary>
public sealed class PotStatusContainer
{
    private readonly Dictionary<string, int> _states = new();

    /// <summary>已激活状态及强度（只读视图，实时反映容器内容）。</summary>
    public IReadOnlyDictionary<string, int> States => _states;

    /// <summary>已激活状态数量。</summary>
    public int Count => _states.Count;

    /// <summary>指定状态是否已激活（强度 &gt; 0）。</summary>
    public bool Has(string statusId) => Get(statusId) > 0;

    /// <summary>读取指定状态的强度；未激活或 ID 非法时返回 0。</summary>
    public int Get(string statusId) =>
        !string.IsNullOrEmpty(statusId) && _states.TryGetValue(statusId, out int value) ? value : 0;

    /// <summary>
    /// 以强度 1 激活指定状态（幂等）：已激活则保持原强度不变，不重复累加。
    /// 物理状态「每锅最多一次」的判定依赖此幂等性。
    /// </summary>
    public void Add(string statusId) => Add(statusId, 1);

    /// <summary>
    /// 以给定强度激活指定状态：取「已有强度」与 <paramref name="level"/> 的较大值（幂等、只增不减）；
    /// <paramref name="level"/> ≤ 0 时移除该状态。
    /// </summary>
    public void Add(string statusId, int level)
    {
        if (string.IsNullOrEmpty(statusId))
            return;

        if (level <= 0)
        {
            _states.Remove(statusId);
            return;
        }

        _states[statusId] = Math.Max(Get(statusId), level);
    }

    /// <summary>设置指定状态的强度（覆盖写）：≤ 0 时移除该状态。</summary>
    public void Set(string statusId, int level)
    {
        if (string.IsNullOrEmpty(statusId))
            return;

        if (level <= 0)
        {
            _states.Remove(statusId);
            return;
        }

        _states[statusId] = level;
    }

    /// <summary>清空全部物理状态（开新锅 / Reset 时调用）。</summary>
    public void Clear() => _states.Clear();
}
