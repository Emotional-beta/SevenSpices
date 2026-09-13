using SevenSpices.Core.Game;
using SevenSpices.Core.Professions;

namespace SevenSpices.Core.Content;

/// <summary>
/// 职业的静态定义，描述「这个味道流派是什么」，不包含运行时状态。
/// 起手规则由 <see cref="Hooks"/> 中的窄接口实现承载，不把规则硬编码进核心流程。
/// </summary>
public class ProfessionDefinition
{
    public string Id { get; }
    public string Name { get; }

    /// <summary>主题味道（七味流派轴）。</summary>
    public FlavorType Theme { get; }

    public string Description { get; }

    /// <summary>起始专属食材 Id（对应 <see cref="IngredientData.ProfessionRegistry"/>）。</summary>
    public string StarterIngredientId { get; }

    /// <summary>起始专属道具 Id（对应 <see cref="ItemData.ProfessionRegistry"/>）。</summary>
    public string StarterItemId { get; }

    /// <summary>
    /// 职业携带的扩展点列表，按顺序调用。
    /// 只允许出现 <c>SevenSpices.Core.Professions</c> 预定义的窄接口实现。
    /// </summary>
    public IReadOnlyList<IProfessionHook> Hooks { get; }

    /// <summary>是否已解锁（V1 恒 true，预留局外解锁）。</summary>
    public bool IsUnlocked { get; }

    public ProfessionDefinition(
        string id,
        string name,
        FlavorType theme,
        string description,
        string starterIngredientId,
        string starterItemId,
        IEnumerable<IProfessionHook>? hooks = null,
        bool isUnlocked = true)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("ProfessionDefinition Id cannot be empty.", nameof(id));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("ProfessionDefinition Name cannot be empty.", nameof(name));
        if (string.IsNullOrWhiteSpace(starterIngredientId))
            throw new ArgumentException("ProfessionDefinition StarterIngredientId cannot be empty.", nameof(starterIngredientId));
        if (string.IsNullOrWhiteSpace(starterItemId))
            throw new ArgumentException("ProfessionDefinition StarterItemId cannot be empty.", nameof(starterItemId));

        Id = id;
        Name = name;
        Theme = theme;
        Description = description ?? string.Empty;
        StarterIngredientId = starterIngredientId;
        StarterItemId = starterItemId;
        IsUnlocked = isUnlocked;

        var hookList = hooks?.ToList() ?? new List<IProfessionHook>();
        // 配置错误尽早暴露：调度侧对不认识的 hook 静默忽略，
        // 若此处不校验，写错接口的 hook 会悄无声息地永不生效。
        foreach (var hook in hookList)
        {
            if (hook is IProfessionPotStartHook
                || hook is IProfessionRuleHook
                || hook is IProfessionVerbHook)
                continue;

            throw new ArgumentException(
                $"ProfessionDefinition '{id}' hook '{hook?.GetType().Name ?? "null"}' implements no known " +
                $"extension point ({nameof(IProfessionPotStartHook)} / {nameof(IProfessionRuleHook)} / " +
                $"{nameof(IProfessionVerbHook)}).",
                nameof(hooks));
        }

        Hooks = hookList.AsReadOnly();
    }
}
