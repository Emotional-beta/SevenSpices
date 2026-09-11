namespace SevenSpices.Core.Customers;

/// <summary>
/// 静态食客定义的集中查询入口。
/// 接收外部传入的 CustomerDefinition 集合，按 Id 建立索引。
/// 不负责创建 Definition、创建 Instance 或加载文件。
/// </summary>
public class CustomerRegistry
{
    private readonly Dictionary<string, CustomerDefinition> _defs;

    public CustomerRegistry(IEnumerable<CustomerDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        _defs = new Dictionary<string, CustomerDefinition>();
        foreach (var def in definitions)
        {
            ArgumentNullException.ThrowIfNull(def);
            if (_defs.ContainsKey(def.Id))
                throw new ArgumentException($"Duplicate CustomerDefinition Id: '{def.Id}'.", nameof(definitions));
            _defs[def.Id] = def;
        }
    }

    /// <summary>
    /// 按 Id 查询 CustomerDefinition。找不到时抛 ArgumentException。
    /// </summary>
    public CustomerDefinition Get(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Id cannot be empty.", nameof(id));

        return _defs.TryGetValue(id, out var def)
            ? def
            : throw new ArgumentException($"CustomerDefinition with Id '{id}' not found.", nameof(id));
    }

    /// <summary>
    /// 返回所有已注册的 CustomerDefinition。
    /// </summary>
    public IReadOnlyList<CustomerDefinition> GetAll() =>
        _defs.Values.ToList().AsReadOnly();
}
