namespace SevenSpices.Core.Companions;

/// <summary>
/// 伙伴的静态定义，描述"这种伙伴是什么"，不包含运行时状态。
/// 行为由 <see cref="Hooks"/> 中的扩展点实现，不把规则硬编码进核心流程。
/// </summary>
public class CompanionDefinition
{
    public string Id { get; }
    public string Name { get; }
    public string Description { get; }

    /// <summary>
    /// 伙伴携带的扩展点列表，按顺序调用。
    /// 只允许出现本命名空间预定义的窄接口实现（见 <see cref="ICompanionHook"/>）。
    /// </summary>
    public IReadOnlyList<ICompanionHook> Hooks { get; }

    public CompanionDefinition(
        string id,
        string name,
        string description = "",
        IEnumerable<ICompanionHook>? hooks = null)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("CompanionDefinition Id cannot be empty.", nameof(id));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("CompanionDefinition Name cannot be empty.", nameof(name));

        Id = id;
        Name = name;
        Description = description ?? string.Empty;

        var hookList = hooks?.ToList() ?? new List<ICompanionHook>();
        // 配置错误尽早暴露：调度侧对不认识的 hook 用 `is not ... continue` 静默忽略，
        // 若此处不校验，写错接口的 hook 会悄无声息地永不生效。要求每个 hook 至少实现
        // 一个已知扩展点（E1 / E2）。
        foreach (var hook in hookList)
        {
            if (hook is IIngredientBaseScoreModifier || hook is IBottomSettlementHook)
                continue;

            throw new ArgumentException(
                $"CompanionDefinition '{id}' hook '{hook?.GetType().Name ?? "null"}' implements no known " +
                $"extension point ({nameof(IIngredientBaseScoreModifier)} / {nameof(IBottomSettlementHook)}).",
                nameof(hooks));
        }

        Hooks = hookList.AsReadOnly();
    }
}
