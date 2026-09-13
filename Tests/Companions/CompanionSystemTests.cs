using SevenSpices.Core.Companions;
using SevenSpices.Core.Content;
using SevenSpices.Core.Effects;
using SevenSpices.Core.Game;
using SevenSpices.Core.Ingredients;
using SevenSpices.Core.Pot;

namespace SevenSpices.Tests.Companions;

/// <summary>
/// 伙伴受控扩展点与 CompanionSystem 测试。
/// 覆盖：无伙伴回归、E1（每个食材 +1）、E2（锅底最高味道 +2）、多伙伴顺序、
/// 异常隔离与防重入等防御行为。
/// </summary>
public static class CompanionSystemTests
{
    public static void RunAll()
    {
        Test_NoCompanions_ReturnsOriginalBaseScore();
        Test_NoCompanions_AddIngredient_Regression();
        Test_GenerousGuest_E1_AddsOneToEachIngredient();
        Test_SweetBoss_E2_ClosePot_AddsTwoToHighestBottom();
        Test_MultipleModifiers_AppliedInAcquisitionOrder();
        Test_ThrowingModifier_IsIsolated_OthersStillApply();
        Test_ReentrantModifier_IsSkipped();
        Test_ThrowingBottomHook_IsIsolated_OthersStillApply();
        Test_SweetBoss_E2_TieBreaksByEnumOrder();
        Test_SweetBoss_E2_AllZero_NoChange();
        Test_Definition_HookWithoutKnownExtensionPoint_Throws();

        Console.WriteLine("All CompanionSystemTests passed.");
    }

    // ── 测试用伙伴 ───────────────────────────────────────────────────────────

    private sealed class LoggingModifier : IIngredientBaseScoreModifier
    {
        private readonly string _name;
        private readonly int _bonus;
        private readonly List<string> _log;

        public LoggingModifier(string name, int bonus, List<string> log)
        {
            _name = name;
            _bonus = bonus;
            _log = log;
        }

        public int ModifyIngredientBaseScore(IngredientInstance ingredient, int baseScore)
        {
            _log.Add(_name);
            return baseScore + _bonus;
        }
    }

    private sealed class ThrowingModifier : IIngredientBaseScoreModifier
    {
        public int ModifyIngredientBaseScore(IngredientInstance ingredient, int baseScore) =>
            throw new InvalidOperationException("boom(modifier)");
    }

    private sealed class ThrowingBottomHook : IBottomSettlementHook
    {
        public void OnBottomSettlement(PotState pot, BottomState bottom) =>
            throw new InvalidOperationException("boom(bottom)");
    }

    /// <summary>不实现任何已知扩展点的空 hook，用于校验 CompanionDefinition 的构造防线。</summary>
    private sealed class UnknownHook : ICompanionHook
    {
    }

    private sealed class ReentrantModifier : IIngredientBaseScoreModifier
    {
        private readonly CompanionSystem _system;

        public int OuterCallCount { get; private set; }

        public ReentrantModifier(CompanionSystem system) => _system = system;

        public int ModifyIngredientBaseScore(IngredientInstance ingredient, int baseScore)
        {
            OuterCallCount++;
            int next = baseScore + 1;
            // 重入调用应被 CompanionSystem 直接跳过（返回入参原值），不得再次进入本 hook。
            _system.ModifyIngredientBaseScore(ingredient, next);
            return next;
        }
    }

    // ── 辅助 ─────────────────────────────────────────────────────────────────

    static CompanionSystem SystemWith(params CompanionDefinition[] definitions)
    {
        var list = new List<CompanionInstance>();
        foreach (var def in definitions)
            list.Add(new CompanionInstance(def));
        return new CompanionSystem(list);
    }

    static CompanionDefinition OneHook(string id, ICompanionHook hook) =>
        new(id, id, hooks: new ICompanionHook[] { hook });

    /// <summary>创建一个处于 IngredientResolve 阶段的 PotController。</summary>
    static PotController MakeAtIngredientResolve(GameState state, CompanionSystem? companions = null)
    {
        var ctrl = new PotController(state, companions);
        ctrl.StartPot();
        ctrl.StartBowl();
        ctrl.AdvanceBowlPhase(); // → Customer
        ctrl.AdvanceBowlPhase(); // → ItemPhase
        ctrl.AdvanceBowlPhase(); // → IngredientSelection
        ctrl.AdvanceBowlPhase(); // → IngredientResolve
        return ctrl;
    }

    static void AddRice(PotController ctrl)
    {
        ctrl.AddIngredient(IngredientData.CreateInstance("rice"), new EffectSystem());
    }

    // ── 测试 ─────────────────────────────────────────────────────────────────

    /// <summary>1. 无伙伴：E1 返回原基础分。</summary>
    static void Test_NoCompanions_ReturnsOriginalBaseScore()
    {
        var system = new CompanionSystem(new List<CompanionInstance>());
        int result = system.ModifyIngredientBaseScore(IngredientData.CreateInstance("rice"), 7);
        Assert(result == 7, $"无伙伴时 E1 应返回原值 7，实际 {result}");
        Assert(system.Active.Count == 0, "无伙伴时 Active 应为空");
    }

    /// <summary>2. 无伙伴：AddIngredient 分数与既有行为一致（米饭 1 → BaseScore 1）。</summary>
    static void Test_NoCompanions_AddIngredient_Regression()
    {
        var state = new GameState();
        var ctrl = MakeAtIngredientResolve(state);
        AddRice(ctrl);
        Assert(state.Pot.BaseScore == 1, $"无伙伴时米饭应贡献 1 分，实际 {state.Pot.BaseScore}");
    }

    /// <summary>3. E1：装备「豪爽客」→ 每个食材贡献 +1（米饭 1 → 2）。</summary>
    static void Test_GenerousGuest_E1_AddsOneToEachIngredient()
    {
        var state = new GameState();
        var system = SystemWith(CompanionData.GenerousGuestCompanion);
        var ctrl = MakeAtIngredientResolve(state, system);
        AddRice(ctrl);
        Assert(state.Pot.BaseScore == 2,
            $"豪爽客应使米饭贡献 2 分（1+1），实际 {state.Pot.BaseScore}");
    }

    /// <summary>4. E2：装备「甜心老板」→ ClosePot 后锅底最高味道比 BottomExtractor 结果 +2。</summary>
    static void Test_SweetBoss_E2_ClosePot_AddsTwoToHighestBottom()
    {
        // 基线：不带伙伴的提炼结果
        var baseline = new GameState();
        var baseCtrl = new PotController(baseline);
        baseCtrl.StartPot();
        baseline.Pot.Flavors[FlavorType.Sweet] = 10;
        baseCtrl.EndPot();
        baseCtrl.ClosePot();
        int baselineSweet = baseline.Bottom.GetFlavor(FlavorType.Sweet);
        Assert(baselineSweet == 3, $"锅底提炼基线：甜 10 → 3，实际 {baselineSweet}");

        // 带甜心老板：锅底最高味道再 +2
        var state = new GameState();
        var system = SystemWith(CompanionData.SweetBossCompanion);
        var ctrl = new PotController(state, system);
        ctrl.StartPot();
        state.Pot.Flavors[FlavorType.Sweet] = 10;
        ctrl.EndPot();
        ctrl.ClosePot();

        Assert(state.Bottom.GetFlavor(FlavorType.Sweet) == baselineSweet + 2,
            $"甜心老板应把最高味道再 +2（{baselineSweet} → {baselineSweet + 2}），实际 {state.Bottom.GetFlavor(FlavorType.Sweet)}");
    }

    /// <summary>5. 多伙伴顺序：按获得顺序依次传递，结果确定、顺序可复现。</summary>
    static void Test_MultipleModifiers_AppliedInAcquisitionOrder()
    {
        var logForward = new List<string>();
        var a = OneHook("A", new LoggingModifier("A", 1, logForward));
        var b = OneHook("B", new LoggingModifier("B", 2, logForward));

        int forward = SystemWith(a, b).ModifyIngredientBaseScore(IngredientData.CreateInstance("rice"), 0);
        Assert(forward == 3, $"A(+1)→B(+2) 应为 3，实际 {forward}");
        Assert(logForward.SequenceEqual(new[] { "A", "B" }),
            $"调用顺序应为 A→B，实际 {string.Join("→", logForward)}");

        var logReverse = new List<string>();
        var ra = OneHook("A", new LoggingModifier("A", 1, logReverse));
        var rb = OneHook("B", new LoggingModifier("B", 2, logReverse));
        int reverse = SystemWith(rb, ra).ModifyIngredientBaseScore(IngredientData.CreateInstance("rice"), 0);
        Assert(reverse == 3, $"B(+2)→A(+1) 应为 3，实际 {reverse}");
        Assert(logReverse.SequenceEqual(new[] { "B", "A" }),
            $"调用顺序应为 B→A，实际 {string.Join("→", logReverse)}");
    }

    /// <summary>6a. 异常隔离：某 E1 hook 抛异常时不影响其它伙伴、不抛到调用方。</summary>
    static void Test_ThrowingModifier_IsIsolated_OthersStillApply()
    {
        var log = new List<string>();
        var good1 = OneHook("good1", new LoggingModifier("good1", 1, log));
        var bad = OneHook("bad", new ThrowingModifier());
        var good2 = OneHook("good2", new LoggingModifier("good2", 2, log));

        int result = SystemWith(good1, bad, good2)
            .ModifyIngredientBaseScore(IngredientData.CreateInstance("rice"), 0);

        Assert(result == 3, $"坏 hook 被跳过，两个好 hook 应得 1+2=3，实际 {result}");
        Assert(log.SequenceEqual(new[] { "good1", "good2" }),
            $"好 hook 应都被调用，实际 {string.Join("→", log)}");
    }

    /// <summary>6b. 防重入：hook 内重入调用被跳过，外层正常返回且只进入一次。</summary>
    static void Test_ReentrantModifier_IsSkipped()
    {
        var list = new List<CompanionInstance>();
        var system = new CompanionSystem(list);
        var hook = new ReentrantModifier(system);
        list.Add(new CompanionInstance(OneHook("reentrant", hook)));

        int result = system.ModifyIngredientBaseScore(IngredientData.CreateInstance("rice"), 0);

        Assert(result == 1, $"外层 hook 应返回 0+1=1，实际 {result}");
        Assert(hook.OuterCallCount == 1, $"重入应被跳过，hook 只应被调用 1 次，实际 {hook.OuterCallCount}");
    }

    /// <summary>6c. E2 异常隔离：某 E2 hook 抛异常时其它 E2 hook 仍生效、不抛到调用方。</summary>
    static void Test_ThrowingBottomHook_IsIsolated_OthersStillApply()
    {
        var bad = OneHook("bad", new ThrowingBottomHook());
        var good = OneHook("good", CompanionData.SweetBossCompanion.Hooks[0]);

        var bottom = new BottomState();
        bottom.SetFlavor(FlavorType.Sweet, 3);

        // 不应抛出异常
        SystemWith(bad, good).ApplyBottomSettlementHooks(new PotState(), bottom);

        Assert(bottom.GetFlavor(FlavorType.Sweet) == 5,
            $"坏 hook 被跳过后，甜心老板应把甜 3 → 5，实际 {bottom.GetFlavor(FlavorType.Sweet)}");
    }

    /// <summary>甜心老板并列最高时取枚举顺序最早的一个（Sour 早于 Umami）。</summary>
    static void Test_SweetBoss_E2_TieBreaksByEnumOrder()
    {
        var bottom = new BottomState();
        bottom.SetFlavor(FlavorType.Umami, 3);
        bottom.SetFlavor(FlavorType.Sour, 3);

        SystemWith(CompanionData.SweetBossCompanion).ApplyBottomSettlementHooks(new PotState(), bottom);

        Assert(bottom.GetFlavor(FlavorType.Sour) == 5,
            $"并列最高应取枚举最早的 Sour +2 = 5，实际 {bottom.GetFlavor(FlavorType.Sour)}");
        Assert(bottom.GetFlavor(FlavorType.Umami) == 3,
            $"Umami 不应被改动，实际 {bottom.GetFlavor(FlavorType.Umami)}");
    }

    /// <summary>锅底全为 0 时甜心老板不写入任何味道。</summary>
    static void Test_SweetBoss_E2_AllZero_NoChange()
    {
        var bottom = new BottomState();
        SystemWith(CompanionData.SweetBossCompanion).ApplyBottomSettlementHooks(new PotState(), bottom);
        foreach (FlavorType flavor in Enum.GetValues<FlavorType>())
            Assert(bottom.GetFlavor(flavor) == 0, $"锅底全 0 时不应写入 {flavor}");
    }

    /// <summary>
    /// 配置防线：Hooks 中含不实现任何已知扩展点的 hook 时，CompanionDefinition 构造应尽早抛异常，
    /// 避免被调度侧 `is not ... continue` 静默忽略；实现已知扩展点的 hook 应被接受。
    /// </summary>
    static void Test_Definition_HookWithoutKnownExtensionPoint_Throws()
    {
        bool threw = false;
        try
        {
            _ = new CompanionDefinition("bad", "坏伙伴", hooks: new ICompanionHook[] { new UnknownHook() });
        }
        catch (ArgumentException) { threw = true; }
        Assert(threw, "含未知扩展点 hook 的 CompanionDefinition 应抛 ArgumentException");

        var ok = new CompanionDefinition(
            "ok", "好伙伴",
            hooks: new ICompanionHook[] { new LoggingModifier("ok", 1, new List<string>()) });
        Assert(ok.Hooks.Count == 1, "实现已知扩展点的 hook 应被接受");
    }

    static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception($"[FAIL] CompanionSystemTests: {message}");
    }
}
