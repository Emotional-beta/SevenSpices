using SevenSpices.Core.Content;
using SevenSpices.Core.Effects;
using SevenSpices.Core.Flavors;
using SevenSpices.Core.Flavors.Verbs;
using SevenSpices.Core.Game;
using SevenSpices.Core.Ingredients;
using SevenSpices.Core.Pot;

namespace SevenSpices.Tests.Flavors;

/// <summary>
/// 味道系统 F2 测试：味道互动层骨架 + 酸·蚀刻 / 甜·复制两个动词。
/// 覆盖：两个动词行为、只在增加时触发、结算顺序、预览一致、异常隔离、防重入、每次加料最多一次。
/// </summary>
public static class FlavorVerbTests
{
    public static void RunAll()
    {
        Test_Etch_TransfersLowestNonZeroOtherFlavor();
        Test_Etch_NoOtherFlavor_NoAction();
        Test_Duplicate_AddsToHighest_WhenSweetIsHighest();
        Test_Duplicate_PicksHighest_NotSweet();
        Test_NoSourIncrement_DoesNotEtch();
        Test_NoSweetIncrement_DoesNotDuplicate();
        Test_Order_EtchBeforeDuplicate();
        Test_Duplicate_ConfigAmount_Adjustable();
        Test_Preview_MatchesRealExecution_WithVerbs();
        Test_ThrowingVerb_IsIsolated_OthersStillRun();
        Test_ReentrantResolve_IsSkipped();
        Test_Verb_AtMostOncePerFlavorPerAdd();

        Console.WriteLine("All FlavorVerbTests passed.");
    }

    // ── 测试用动词 ───────────────────────────────────────────────────────────

    private sealed class ThrowingVerb : IFlavorVerb
    {
        public void Apply(FlavorContext context) =>
            throw new InvalidOperationException("boom(verb)");
    }

    private sealed class CountingSelfGrowingVerb : IFlavorVerb
    {
        public int ApplyCount { get; private set; }

        public void Apply(FlavorContext context)
        {
            ApplyCount++;
            context.Pot.AddFlavor(context.Flavor, 1);
        }
    }

    private sealed class ReentrantVerb : IFlavorVerb
    {
        /// <summary>包含本动词的互动系统，在系统构造后回填（字典构造时无法自引用）。</summary>
        public FlavorInteractionSystem? System { get; set; }

        public int ApplyCount { get; private set; }

        public void Apply(FlavorContext context)
        {
            ApplyCount++;
            // 重入调用应被 FlavorInteractionSystem 直接跳过，不得再次进入本动词。
            System?.Resolve(context.Pot, context.Source);
            context.Pot.AddFlavor(context.Flavor, 1);
        }
    }

    // ── 辅助 ─────────────────────────────────────────────────────────────────

    static PotController MakeControllerAtIngredientResolve(out GameState state, FlavorInteractionSystem? system = null)
    {
        state = new GameState();
        var ctrl = new PotController(state, flavorInteraction: system);
        ctrl.StartPot();
        ctrl.StartBowl();
        ctrl.AdvanceBowlPhase(); // → Customer
        ctrl.AdvanceBowlPhase(); // → ItemPhase
        ctrl.AdvanceBowlPhase(); // → IngredientSelection
        ctrl.AdvanceBowlPhase(); // → IngredientResolve
        return ctrl;
    }

    static IngredientInstance MakeIngredient(int baseScore, params (FlavorType Flavor, int Amount)[] flavors)
    {
        var dict = new Dictionary<FlavorType, int>();
        foreach (var (flavor, amount) in flavors)
            dict[flavor] = amount;

        var def = new IngredientDefinition(
            id: "test_" + Guid.NewGuid().ToString("N"),
            name: "测试食材",
            rarity: IngredientRarity.Common,
            baseScore: baseScore,
            flavors: dict);
        return new IngredientInstance(def);
    }

    static FlavorInteractionSystem SystemWith(params (FlavorType Flavor, IFlavorVerb Verb)[] verbs)
    {
        var dict = new Dictionary<FlavorType, IFlavorVerb>();
        foreach (var (flavor, verb) in verbs)
            dict[flavor] = verb;
        return new FlavorInteractionSystem(dict);
    }

    // ── 1. 酸·蚀刻 ───────────────────────────────────────────────────────────

    static void Test_Etch_TransfersLowestNonZeroOtherFlavor()
    {
        var ctrl = MakeControllerAtIngredientResolve(out var state);
        state.Pot.AddFlavor(FlavorType.Spicy, 3);
        state.Pot.AddFlavor(FlavorType.Umami, 1);

        ctrl.AddIngredient(MakeIngredient(0, (FlavorType.Sour, 1)), new EffectSystem());

        Assert(state.Pot.GetFlavor(FlavorType.Sour) == 2, "酸应变为 2（自身 +1，再蚀入鲜 1）");
        Assert(state.Pot.GetFlavor(FlavorType.Umami) == 0, "最低非零其他味道（鲜 1）应被蚀为 0");
        Assert(state.Pot.GetFlavor(FlavorType.Spicy) == 3, "辣 3 不应被改动");
        // 提鲜系数为派生只读：鲜被蚀为 0 后提鲜失效，味道分 = 酸 2 + 辣 3 = 5.0。
        Assert(state.Pot.FlavorScore == 5.0,
            $"蚀刻把鲜转移到酸并使提鲜随之失效，味道分应为 5.0，实际 {state.Pot.FlavorScore}");
    }

    // ── 2. 酸·蚀刻边界 ───────────────────────────────────────────────────────

    static void Test_Etch_NoOtherFlavor_NoAction()
    {
        var ctrl = MakeControllerAtIngredientResolve(out var state);
        state.Pot.AddFlavor(FlavorType.Sour, 1); // 锅内仅有酸

        var es = new EffectSystem();
        ctrl.AddIngredient(MakeIngredient(0, (FlavorType.Sour, 1)), es);

        Assert(state.Pot.GetFlavor(FlavorType.Sour) == 2, "无其它非零味道时酸动词不动作，酸应为 1+1=2");
        foreach (FlavorType flavor in Enum.GetValues<FlavorType>())
        {
            if (flavor == FlavorType.Sour) continue;
            Assert(state.Pot.GetFlavor(flavor) == 0, $"无其它非零味道时不应凭空产生 {flavor}");
        }
    }

    // ── 3. 甜·复制（甜为最高） ────────────────────────────────────────────────

    static void Test_Duplicate_AddsToHighest_WhenSweetIsHighest()
    {
        var ctrl = MakeControllerAtIngredientResolve(out var state);
        state.Pot.AddFlavor(FlavorType.Sweet, 1);

        ctrl.AddIngredient(MakeIngredient(0, (FlavorType.Sweet, 1)), new EffectSystem());

        Assert(state.Pot.GetFlavor(FlavorType.Sweet) == 3,
            "甜先加为 2，复制最高（甜）一份 → 3");
    }

    // ── 4. 甜·复制取最高 ─────────────────────────────────────────────────────

    static void Test_Duplicate_PicksHighest_NotSweet()
    {
        var ctrl = MakeControllerAtIngredientResolve(out var state);
        state.Pot.AddFlavor(FlavorType.Sweet, 1);
        state.Pot.AddFlavor(FlavorType.Spicy, 5);

        ctrl.AddIngredient(MakeIngredient(0, (FlavorType.Sweet, 1)), new EffectSystem());

        Assert(state.Pot.GetFlavor(FlavorType.Sweet) == 2, "甜先加为 2，但最高是辣 5，甜不应被复制");
        Assert(state.Pot.GetFlavor(FlavorType.Spicy) == 6, "最高味道辣 5 应 +1 → 6");
    }

    // ── 5. 只在增加时触发 ─────────────────────────────────────────────────────

    static void Test_NoSourIncrement_DoesNotEtch()
    {
        var ctrl = MakeControllerAtIngredientResolve(out var state);
        state.Pot.AddFlavor(FlavorType.Sour, 1);
        state.Pot.AddFlavor(FlavorType.Umami, 1);

        // 本次不加酸 → 即使锅内酸 > 0，酸动词也不应触发。
        // 用「咸」作为对照味道：咸·固化不消耗咸值，数值可确定性断言。
        ctrl.AddIngredient(MakeIngredient(0, (FlavorType.Salty, 1)), new EffectSystem());

        Assert(state.Pot.GetFlavor(FlavorType.Sour) == 1, "酸未增加 → 酸应保持 1");
        Assert(state.Pot.GetFlavor(FlavorType.Umami) == 1, "酸动词未触发 → 鲜不应被蚀");
        Assert(state.Pot.GetFlavor(FlavorType.Salty) == 1, "咸应正常 +1");
    }

    static void Test_NoSweetIncrement_DoesNotDuplicate()
    {
        var ctrl = MakeControllerAtIngredientResolve(out var state);
        state.Pot.AddFlavor(FlavorType.Sweet, 1);

        // 本次不加甜 → 即使锅内甜 > 0，甜动词也不应触发。
        ctrl.AddIngredient(MakeIngredient(0, (FlavorType.Umami, 1)), new EffectSystem());

        Assert(state.Pot.GetFlavor(FlavorType.Sweet) == 1, "甜未增加 → 甜应保持 1（不被复制）");
        Assert(state.Pot.GetFlavor(FlavorType.Umami) == 1, "鲜应正常 +1");
    }

    // ── 6. 顺序确定：酸先于甜 ─────────────────────────────────────────────────

    static void Test_Order_EtchBeforeDuplicate()
    {
        var ctrl = MakeControllerAtIngredientResolve(out var state);
        // 构造使两种结算顺序结果不同的场景：非酸味道全部并列且都高于甜。
        state.Pot.AddFlavor(FlavorType.Bitter, 2);
        state.Pot.AddFlavor(FlavorType.Umami, 2);
        state.Pot.AddFlavor(FlavorType.Spicy, 2);

        ctrl.AddIngredient(
            MakeIngredient(0, (FlavorType.Sour, 1), (FlavorType.Sweet, 1)),
            new EffectSystem());

        // 正确顺序（酸→甜）：先蚀刻吃掉最低的甜 1 → 酸 2、甜 0；
        // 复制时最高并列于 2（酸/苦/鲜/辣），取枚举最早的酸 → 酸 3。
        Assert(state.Pot.GetFlavor(FlavorType.Sour) == 3, "先蚀刻后复制：酸应为 3");
        Assert(state.Pot.GetFlavor(FlavorType.Sweet) == 0, "先蚀刻后复制：甜应被蚀为 0");
        Assert(state.Pot.GetFlavor(FlavorType.Bitter) == 2, "苦应保持 2（若顺序颠倒会被复制为 3）");
        Assert(state.Pot.GetFlavor(FlavorType.Umami) == 2, "鲜应保持 2");
        Assert(state.Pot.GetFlavor(FlavorType.Spicy) == 2, "辣应保持 2");
    }

    // ── 数据驱动：复制份数可调 ───────────────────────────────────────────────

    static void Test_Duplicate_ConfigAmount_Adjustable()
    {
        var pot = new PotState { Config = new FlavorConfig { SweetDuplicateAmount = 2 } };
        pot.AddFlavor(FlavorType.Sweet, 2); // 模拟基础味道已应用（已有 1 + 本次 1）
        var added = MakeIngredient(0, (FlavorType.Sweet, 1));

        // 配置从 pot.Config 读取（统一来源）。
        FlavorInteractionSystem.Default.Resolve(pot, added);

        Assert(pot.GetFlavor(FlavorType.Sweet) == 4, "复制份数配置为 2 时甜应为 2 + 2 = 4");
    }

    // ── 7. 预览一致 ──────────────────────────────────────────────────────────

    static void Test_Preview_MatchesRealExecution_WithVerbs()
    {
        static void Setup(GameState state)
        {
            state.Pot.AddFlavor(FlavorType.Bitter, 2);
            state.Pot.AddFlavor(FlavorType.Umami, 2);
            state.Pot.AddFlavor(FlavorType.Spicy, 2);
        }

        // 预测路径
        var previewCtrl = MakeControllerAtIngredientResolve(out var previewState);
        Setup(previewState);
        var preview = previewCtrl.PreviewIngredient(
            MakeIngredient(0, (FlavorType.Sour, 1), (FlavorType.Sweet, 1)), new EffectSystem());

        // 真实路径
        var realCtrl = MakeControllerAtIngredientResolve(out var realState);
        Setup(realState);
        realCtrl.AddIngredient(
            MakeIngredient(0, (FlavorType.Sour, 1), (FlavorType.Sweet, 1)), new EffectSystem());
        realCtrl.AdvanceBowlPhase(); // → ScoreCalculation
        realCtrl.CalculateScore();

        Assert(preview.PreviewFinalScore == realState.Pot.FinalScore,
            $"含动词的预览({preview.PreviewFinalScore}) 应与真实结算({realState.Pot.FinalScore}) 一致");
        Assert(previewState.Pot.GetFlavor(FlavorType.Sour) == 0,
            "预览不得修改真实锅的酸");
    }

    // ── 8. 异常隔离 ──────────────────────────────────────────────────────────

    static void Test_ThrowingVerb_IsIsolated_OthersStillRun()
    {
        var system = SystemWith(
            (FlavorType.Sour, new ThrowingVerb()),
            (FlavorType.Sweet, new DuplicateVerb()));

        var ctrl = MakeControllerAtIngredientResolve(out var state, system);
        state.Pot.AddFlavor(FlavorType.Sweet, 5);

        // 不应抛出异常
        ctrl.AddIngredient(
            MakeIngredient(0, (FlavorType.Sour, 1), (FlavorType.Sweet, 1)), new EffectSystem());

        Assert(state.Pot.GetFlavor(FlavorType.Sour) == 1,
            "抛异常的酸动词不改变状态：酸应保持 1");
        Assert(state.Pot.GetFlavor(FlavorType.Sweet) == 8,
            "坏动词被跳过后甜动词仍执行：甜 6 达到 F4 超频档位（potency 2）→ 最高甜再复制 +2 = 8");
    }

    // ── 防重入 ───────────────────────────────────────────────────────────────

    static void Test_ReentrantResolve_IsSkipped()
    {
        var verb = new ReentrantVerb();
        var system = SystemWith((FlavorType.Sour, verb));
        verb.System = system;

        var pot = new PotState();
        pot.AddFlavor(FlavorType.Sour, 1); // 模拟基础味道已应用
        var added = MakeIngredient(0, (FlavorType.Sour, 1));

        system.Resolve(pot, added);

        Assert(verb.ApplyCount == 1, $"重入应被跳过，动词只应执行 1 次，实际 {verb.ApplyCount}");
        Assert(pot.GetFlavor(FlavorType.Sour) == 2, "外层动词只应 +1：酸应为 2");
    }

    // ── 9. 每次加料最多一次 ──────────────────────────────────────────────────

    static void Test_Verb_AtMostOncePerFlavorPerAdd()
    {
        var sourVerb = new CountingSelfGrowingVerb();
        var sweetVerb = new CountingSelfGrowingVerb();
        var system = SystemWith(
            (FlavorType.Sour, sourVerb),
            (FlavorType.Sweet, sweetVerb));

        var ctrl = MakeControllerAtIngredientResolve(out var state, system);
        ctrl.AddIngredient(
            MakeIngredient(0, (FlavorType.Sour, 1), (FlavorType.Sweet, 1)), new EffectSystem());

        Assert(sourVerb.ApplyCount == 1, $"酸动词每次加料只应结算 1 次，实际 {sourVerb.ApplyCount}");
        Assert(sweetVerb.ApplyCount == 1, $"甜动词每次加料只应结算 1 次，实际 {sweetVerb.ApplyCount}");
        Assert(state.Pot.GetFlavor(FlavorType.Sour) == 2,
            "酸应为 基础1 + 动词一次1 = 2（若重复结算会继续增长）");
        Assert(state.Pot.GetFlavor(FlavorType.Sweet) == 2,
            "甜应为 基础1 + 动词一次1 = 2（若重复结算会继续增长）");
    }

    // ─────────────────────────────────────────────────────────────────────────

    static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception($"[FAIL] FlavorVerbTests: {message}");
    }
}
