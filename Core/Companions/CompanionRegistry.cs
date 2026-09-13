namespace SevenSpices.Core.Companions;

/// <summary>
/// 静态伙伴定义的集中查询入口。
/// 接收外部传入的 CompanionDefinition 集合，按 Id 建立索引。
/// 不负责创建 Definition、创建 Instance 或加载文件。
/// </summary>
public class CompanionRegistry
{
    private readonly Dictionary<string, CompanionDefinition> _defs;

    public CompanionRegistry(IEnumerable<CompanionDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        _defs = new Dictionary<string, CompanionDefinition>();
        foreach (var def in definitions)
        {
            ArgumentNullException.ThrowIfNull(def);
            if (_defs.ContainsKey(def.Id))
                throw new ArgumentException($"Duplicate CompanionDefinition Id: '{def.Id}'.", nameof(definitions));
            _defs[def.Id] = def;
        }
    }

    /// <summary>按 Id 查询 CompanionDefinition。找不到时抛 ArgumentException。</summary>
    public CompanionDefinition Get(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Id cannot be empty.", nameof(id));

        return _defs.TryGetValue(id, out var def)
            ? def
            : throw new ArgumentException($"CompanionDefinition with Id '{id}' not found.", nameof(id));
    }

    /// <summary>
    /// 按 Id 查询 CompanionDefinition；id 为空 / 未注册时返回 null（用于存档恢复等「非致命」查找）。
    /// 与 <see cref="Get"/> 不同，未命中不抛异常。
    /// </summary>
    public CompanionDefinition? Find(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        return _defs.TryGetValue(id, out var def) ? def : null;
    }

    /// <summary>返回所有已注册的 CompanionDefinition。</summary>
    public IReadOnlyList<CompanionDefinition> GetAll() =>
        _defs.Values.ToList().AsReadOnly();
}
