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
/// <item>动词结算后，作为独立步骤执行麻·共振（麻自身不注册普通动词）。</item>
/// <item>苦·陈酿的增长 / 到期也在每次加料后统一推进，保证预览一致。</item>
/// <item>提鲜系数已改为 PotState 的派生只读属性，无需在加料后刷新。</item>
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

    /// <summary>默认互动系统：酸→蚀刻、甜→复制、苦→陈酿、辣→余温、鲜→提鲜、咸→固化；麻不注册普通动词。</summary>
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
    /// <para>
    /// 配置一律从 <see cref="PotState.Config"/> 读取（由 PotController / RunController 在开锅时注入），
    /// 不再接受外部 config 参数，避免「惰性恢复路径」下外部配置与锅上配置不一致导致漂移。
    /// </para>
    /// </summary>
    /// <param name="baseScoreAdded">
    /// 本次加料对基础分的增量（已含伙伴等修正），供苦·陈酿按比例存入。默认 0。
    /// </param>
    public void Resolve(PotState pot, IngredientInstance added, int baseScoreAdded = 0)
    {
        ArgumentNullException.ThrowIfNull(pot);
        ArgumentNullException.ThrowIfNull(added);

        // 防重入：重入调用直接跳过，避免递归结算 / 状态半更新。
        if (_resolving)
            return;

        _resolving = true;
        try
        {
            var config = pot.Config;

            // 第 2 步：物理状态检查（如「臭」）。必须早于味道动词结算（设计文档 §11.1）。
            FlavorStatusRules.CheckPhysicalStates(pot, config);

            // 遍历前快照：以「本次增加的味道集合」为准，不随动词对锅的改动而漂移。
            var increased = GetIncreasedFlavors(added);

            // 第 3 步：味道动词结算（酸 → 甜 → 苦 → 辣 → 鲜 → 咸）。
            foreach (FlavorType flavor in Enum.GetValues<FlavorType>())
            {
                if (!increased.Contains(flavor))
                    continue;

                ApplyVerbSafe(flavor, pot, added, baseScoreAdded);
            }

            // 苦·陈酿：每次加料推进池的增长 / 到期判定（存入由苦动词完成）。
            AgingVerb.AdvanceCycle(pot, config);

            // 第 4 步：麻·共振（作为独立步骤，在动词结算之后）。
            if (increased.Contains(FlavorType.Numbing))
                ApplyResonance(pot, added);
        }
        finally
        {
            _resolving = false;
        }
    }

    /// <summary>
    /// 结算单个味道的动词：未注册 / 抛异常时静默跳过，绝不影响其余味道与核心流程。
    /// 直接调用动词（不走 <see cref="Resolve"/>），因此天然绕过防重入守卫，供共振与主循环共用。
    /// 动词上下文中的配置取自 <see cref="PotState.Config"/>。
    /// </summary>
    private bool ApplyVerbSafe(FlavorType flavor, PotState pot, IngredientInstance added, int baseScoreAdded)
    {
        if (!_verbs.TryGetValue(flavor, out var verb))
            return false;

        try
        {
            verb.Apply(new FlavorContext(pot, added, flavor, pot.Config, baseScoreAdded));
        }
        catch
        {
            // 单个动词异常：保留当前状态、跳过该动词，继续其余味道，绝不污染核心流程。
        }

        return true;
    }

    /// <summary>
    /// 麻·共振：在<b>已注册普通动词</b>的味道中取当前最高（并列取枚举最早），
    /// 按其份数（封顶 <see cref="FlavorConfig.NumbingResonanceCap"/>）再触发该味道的动词 N 次。
    /// <para>
    /// 麻自身不注册普通动词，故天然不会被选为目标；若没有任何已注册动词的味道则空转。
    /// 每次调用都直接调动词（不经 <see cref="Resolve"/>），故不会递归回共振步骤，
    /// 配合封顶天然不会无限自我触发（设计文档 §11.1）。共振触发的基础分增量为 0。
    /// </para>
    /// </summary>
    private void ApplyResonance(PotState pot, IngredientInstance added)
    {
        FlavorType target = default;
        int highest = 0;

        // Enum.GetValues 按声明顺序返回；仅在严格更大时更新，故并列时保留最早的一个。
        // 只考虑已注册普通动词的味道，避免目标为麻等无动词味道而空转。
        foreach (FlavorType flavor in Enum.GetValues<FlavorType>())
        {
            if (!_verbs.ContainsKey(flavor))
                continue;

            int value = pot.GetFlavor(flavor);
            if (value > highest)
            {
                highest = value;
                target = flavor;
            }
        }

        if (highest <= 0)
            return;

        int triggers = Math.Min(highest, pot.Config.NumbingResonanceCap);
        for (int i = 0; i < triggers; i++)
            ApplyVerbSafe(target, pot, added, baseScoreAdded: 0);
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
            [FlavorType.Bitter] = new AgingVerb(),
            [FlavorType.Spicy] = new HeatVerb(),
            [FlavorType.Umami] = new UmamiVerb(),
            [FlavorType.Salty] = new SolidifyVerb(),
        };
        return new ReadOnlyDictionary<FlavorType, IFlavorVerb>(verbs);
    }
}
