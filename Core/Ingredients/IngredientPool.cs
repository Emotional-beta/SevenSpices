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
    /// </summary>
    public IReadOnlyList<IngredientInstance> Draw(int maxCount = 3)
    {
        if (maxCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxCount), "maxCount must be positive.");

        int count = Math.Min(maxCount, _instances.Count);
        var candidates = new List<IngredientInstance>(count);

        // 无放回：从池中取出放入候选，不重放回直到玩家做出选择
        var available = new List<IngredientInstance>(_instances);
        for (int i = 0; i < count; i++)
        {
            int idx = _random.Next(available.Count);
            candidates.Add(available[idx]);
            available.RemoveAt(idx);
        }

        // 临时从池中移除候选，等待玩家确认
        foreach (var c in candidates)
            _instances.Remove(c);

        return candidates;
    }

    /// <summary>
    /// 玩家从候选中选择一个实例。未选中的返回池中，选中的不再返回。
    /// </summary>
    public IngredientInstance Confirm(IReadOnlyList<IngredientInstance> candidates, string selectedInstanceId)
    {
        ArgumentNullException.ThrowIfNull(candidates);
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

        return selected;
    }

    /// <summary>
    /// 取消本次抽取，将所有候选全部返回池中。
    /// </summary>
    public void ReturnCandidates(IReadOnlyList<IngredientInstance> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        foreach (var c in candidates)
            _instances.Add(c);
    }

    public IReadOnlyList<IngredientInstance> GetAll() => _instances.AsReadOnly();
}
