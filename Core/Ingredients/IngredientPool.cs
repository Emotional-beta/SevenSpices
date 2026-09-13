using SevenSpices.Core.Common;
using SevenSpices.Core.Ingredients;

namespace SevenSpices.Core.Ingredients;

/// <summary>
/// 当前可抽取的食材实例池。
/// 无放回抽取，最多抽3个；被选中的实例从池中移除，未选中的返回池中。
/// </summary>
public class IngredientPool
{
    private readonly List<IngredientInstance> _instances;
    private readonly Random _random;

    /// <summary>已抽出但尚未 Confirm / ReturnCandidates 结算的候选批；null 表示当前无未结算候选。</summary>
    private List<IngredientInstance>? _pending;

    public int Count => _instances.Count;

    public IngredientPool(IEnumerable<IngredientInstance>? initial = null, Random? random = null)
    {
        _instances = initial != null ? new List<IngredientInstance>(initial) : new List<IngredientInstance>();
        _random = random ?? Random.Shared;
    }

    public void Add(IngredientInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        _instances.Add(instance);
    }

    /// <summary>
    /// 无放回抽取，最多 maxCount 个。不足则全部抽出。
    /// 返回候选列表——调用方必须在选择后调用 Confirm 或 ReturnCandidates。
    /// 同一时间只允许存在一批未结算候选；上一批未结算（<see cref="Confirm"/> / <see cref="ReturnCandidates"/>
    /// 之前）再次 Draw 会抛 <see cref="InvalidOperationException"/>，不再静默丢弃上一批候选。
    /// 池空时抽出的空批不视为未结算候选，不阻塞后续抽取。
    /// <para>
    /// <paramref name="weightSelector"/> 为可选的食材权重函数（路线系统「餐饮风潮」倾斜用）：
    /// 为 null 或权重全等时走原有 <see cref="Random.Next(int)"/> 路径，随机数消耗与旧行为逐位一致；
    /// 否则按累积权重法（<see cref="Random.NextDouble"/>）无放回抽取。
    /// </para>
    /// </summary>
    public IReadOnlyList<IngredientInstance> Draw(
        int maxCount = 3,
        Func<IngredientDefinition, double>? weightSelector = null)
    {
        if (_pending is { Count: > 0 })
            throw new InvalidOperationException(
                "Cannot draw: previous candidates are still unresolved. " +
                "Call Confirm or ReturnCandidates before drawing again.");

        if (maxCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxCount), "maxCount must be positive.");

        int count = Math.Min(maxCount, _instances.Count);
        var candidates = new List<IngredientInstance>(count);

        // 无放回：从池中取出放入候选，不重放回直到玩家做出选择
        var available = new List<IngredientInstance>(_instances);

        Func<IngredientInstance, double>? itemWeight = weightSelector == null
            ? null
            : instance => weightSelector(instance.Definition);
        bool weighted = !WeightedRandom.IsUniform(available, itemWeight);

        for (int i = 0; i < count; i++)
        {
            int idx = weighted
                ? WeightedRandom.PickIndex(available, itemWeight!, _random)
                : _random.Next(available.Count);
            candidates.Add(available[idx]);
            available.RemoveAt(idx);
        }

        // 临时从池中移除候选，等待玩家确认
        foreach (var c in candidates)
            _instances.Remove(c);

        // 空批（池空）不算未结算候选，避免池空连续抽时误触发守卫。
        _pending = candidates.Count > 0 ? candidates : null;

        return candidates;
    }

    /// <summary>
    /// 玩家从候选中选择一个实例。未选中的返回池中，选中的不再返回。
    /// </summary>
    public IngredientInstance Confirm(IReadOnlyList<IngredientInstance> candidates, string selectedInstanceId)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ValidatePendingBatch(candidates);
        if (string.IsNullOrWhiteSpace(selectedInstanceId))
            throw new ArgumentException("selectedInstanceId cannot be empty.", nameof(selectedInstanceId));

        var selected = candidates.FirstOrDefault(c => c.InstanceId == selectedInstanceId)
            ?? throw new ArgumentException($"Instance '{selectedInstanceId}' not found in candidates.", nameof(selectedInstanceId));

        // 未选中的全部返回池
        foreach (var c in candidates)
        {
            if (c.InstanceId != selectedInstanceId)
                _instances.Add(c);
        }

        _pending = null;
        return selected;
    }

    /// <summary>
    /// 取消本次抽取，将所有候选全部返回池中。
    /// </summary>
    public void ReturnCandidates(IReadOnlyList<IngredientInstance> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ValidatePendingBatch(candidates);
        foreach (var c in candidates)
            _instances.Add(c);

        _pending = null;
    }

    /// <summary>
    /// 校验传入批次与当前未结算候选（<see cref="_pending"/>）一致：数量相等且两侧 InstanceId 集合完全相等。
    /// 内容一致即可（不要求引用相等），因为调用方常把 <see cref="Draw"/> 的返回值拷入自己的字段再传回。
    /// 双向集合校验可挡住「数量相等但含重复 / 缺失」的批次（如 pending=[A,B,C] 传入 [A,A,B]）。
    /// 校验失败时抛 <see cref="InvalidOperationException"/> 且<b>不清除</b> pending，避免未结算候选被永久丢失。
    /// </summary>
    private void ValidatePendingBatch(IReadOnlyList<IngredientInstance> candidates)
    {
        if (_pending == null)
            throw new InvalidOperationException(
                "No unresolved candidate batch to settle. " +
                "Confirm/ReturnCandidates must receive the batch returned by the most recent Draw.");

        if (candidates.Count != _pending.Count)
            throw new InvalidOperationException(
                $"Candidate batch has {candidates.Count} item(s) but the pending batch has {_pending.Count}. " +
                "Confirm/ReturnCandidates must receive the batch returned by the most recent Draw.");

        var pendingIds = _pending.Select(c => c.InstanceId).ToHashSet();
        var candidateIds = candidates.Select(c => c.InstanceId).ToHashSet();
        if (!pendingIds.SetEquals(candidateIds))
            throw new InvalidOperationException(
                "Candidate batch does not match the pending batch: InstanceId sets differ. " +
                "Confirm/ReturnCandidates must receive the batch returned by the most recent Draw.");
    }

    public IReadOnlyList<IngredientInstance> GetAll() => _instances.AsReadOnly();
}
