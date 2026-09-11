namespace SevenSpices.Core.Ingredients;

/// <summary>
/// 静态食材定义的集中查询入口。
/// 接收外部传入的 IngredientDefinition 集合，按 Id 建立索引。
/// 不负责创建 Definition、创建 Instance 或加载文件。
/// </summary>
public class IngredientRegistry
{
    private readonly Dictionary<string, IngredientDefinition> _defs;

    public IngredientRegistry(IEnumerable<IngredientDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        _defs = new Dictionary<string, IngredientDefinition>();
        foreach (var def in definitions)
        {
            ArgumentNullException.ThrowIfNull(def);
            if (_defs.ContainsKey(def.Id))
                throw new ArgumentException($"Duplicate IngredientDefinition Id: '{def.Id}'.", nameof(definitions));
            _defs[def.Id] = def;
        }
    }

    /// <summary>
    /// 按 Id 查询 IngredientDefinition。找不到时抛 ArgumentException。
    /// </summary>
    public IngredientDefinition Get(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Id cannot be empty.", nameof(id));

        return _defs.TryGetValue(id, out var def)
            ? def
            : throw new ArgumentException($"IngredientDefinition with Id '{id}' not found.", nameof(id));
    }

    /// <summary>
    /// 返回所有已注册的 IngredientDefinition，顺序与构造时传入顺序一致。
    /// </summary>
    public IReadOnlyList<IngredientDefinition> GetAll() =>
        _defs.Values.ToList().AsReadOnly();
}
