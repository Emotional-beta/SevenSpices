using System.Collections.ObjectModel;
using SevenSpices.Core.Content;
using SevenSpices.Core.Game;
using SevenSpices.Core.Ingredients;
using SevenSpices.Core.Flavors.Verbs;

namespace SevenSpices.Core.Flavors;

/// <summary>
/// 味道互动层的唯一调度入口：在「食材基础数据应用后、食材特殊效果前」结算味道动词。
/// <para>
/// 结算规则（设计文档 §11.1）：
/// <list type="number">
/// <item>只处理本次加料使该味道<b>增加</b>的味道（<c>Definition.Flavors</c> 中值为正）。</item>
/// <item>按 <see cref="FlavorType"/> 声明顺序遍历：酸 → 甜 → 苦 → 辣 → 鲜 → 咸 → 麻。</item>
/// <item>每种味道的动词每次加料最多结算一次；未注册动词的味道直接跳过。</item>
/// </list>
/// </para>
/// <para>
/// <b>防御三件套</b>（与 <c>CompanionSystem</c> 同范式）：
/// <list type="number">
/// <item><b>快照</b>：遍历前先把「本次增加的味道集合」算好，遍历以快照为准。</item>
/// <item><b>异常隔离</b>：单个动词抛异常时保留当前状态、跳过该动词，不抛给调用方。</item>
/// <item><b>防重入</b>：一次 Resolve 未结束时若被重入调用，直接跳过。</item>
/// </list>
/// </para>
/// 纯 C#，不依赖 Godot / 场景节点。
/// </summary>
public class FlavorInteractionSystem
{
    private readonly IReadOnlyDictionary<FlavorType, IFlavorVerb> _verbs;

    /// <summary>防重入标志：true 表示当前有一次 Resolve 正在进行；重入调用被静默跳过。</summary>
    private bool _resolving;

    /// <summary>默认互动系统：酸→蚀刻、甜→复制，其余味道暂缺（遇到即跳过）。</summary>
    public static FlavorInteractionSystem Default { get; } = new();

    /// <param name="verbs">
    /// 味道 → 动词的注册表。为空时使用默认注册表。字典在构造时被只读包装。
    /// </param>
    public FlavorInteractionSystem(IReadOnlyDictionary<FlavorType, IFlavorVerb>? verbs = null)
    {
        _verbs = verbs ?? BuildDefaultVerbs();
    }

    /// <summary>当前注册的味道动词（只读）。</summary>
    public IReadOnlyDictionary<FlavorType, IFlavorVerb> Verbs => _verbs;

    /// <summary>
    /// 在加料瞬间结算味道互动层。
    /// 调用方须先应用食材基础分与基础味道值，再调用本方法；食材自身特殊效果应在之后触发。
    /// </summary>
    public void Resolve(PotState pot, IngredientInstance added, FlavorConfig config)
    {
        ArgumentNullException.ThrowIfNull(pot);
        ArgumentNullException.ThrowIfNull(added);
        ArgumentNullException.ThrowIfNull(config);

        // 防重入：重入调用直接跳过，避免递归结算 / 状态半更新。
        if (_resolving)
            return;

        _resolving = true;
        try
        {
            // 遍历前快照：以「本次增加的味道集合」为准，不随动词对锅的改动而漂移。
            var increased = GetIncreasedFlavors(added);

            foreach (FlavorType flavor in Enum.GetValues<FlavorType>())
            {
                if (!increased.Contains(flavor))
                    continue;
                if (!_verbs.TryGetValue(flavor, out var verb))
                    continue;

                try
                {
                    verb.Apply(new FlavorContext(pot, added, flavor, config));
                }
                catch
                {
                    // 单个动词异常：保留当前状态、跳过该动词，继续其余味道，绝不污染核心流程。
                }
            }
        }
        finally
        {
            _resolving = false;
        }
    }

    private static HashSet<FlavorType> GetIncreasedFlavors(IngredientInstance added)
    {
        var increased = new HashSet<FlavorType>();
        foreach (var (flavor, amount) in added.Definition.Flavors)
        {
            if (amount > 0)
                increased.Add(flavor);
        }
        return increased;
    }

    private static IReadOnlyDictionary<FlavorType, IFlavorVerb> BuildDefaultVerbs()
    {
        var verbs = new Dictionary<FlavorType, IFlavorVerb>
        {
            [FlavorType.Sour] = new EtchVerb(),
            [FlavorType.Sweet] = new DuplicateVerb(),
        };
        return new ReadOnlyDictionary<FlavorType, IFlavorVerb>(verbs);
    }
}
