using SevenSpices.Core.Content;
using SevenSpices.Core.Customers;
using SevenSpices.Core.Effects;
using SevenSpices.Core.Events;
using SevenSpices.Core.Game;
using SevenSpices.Core.Items;
using SevenSpices.Core.Pot;
using SevenSpices.Core.Run;
using SevenSpices.Core.Scoring;

namespace SevenSpices.Tests.Run;

/// <summary>
/// 饕餮 Boss 测试（方案 §八 B0 + B1 + B2）。
/// B0/B1 覆盖数据地基：MetaState、TotalFinalScore、新条件类型、饕餮定义、
/// BossConfig、仙丹粉末与 SpecialRegistry、AddAllFlavorsEffect；
/// B2 覆盖章末接入：章末指派、跨章触发、满意赏赐、嫌弃终止与本局记录。
/// </summary>
public static class BossSettlementTests
{
    public static void RunAll()
    {
        // B0：跨局保留容器
        Test_MetaState_KeepsAtMostOne();
        Test_MetaState_NullThrows();
        Test_GameController_MetaState_Injection();

        // B1：本锅累计最终分
        Test_TotalFinalScore_AccumulatesFinalScoreWithMultiplier();
        Test_TotalFinalScore_EndToEndAccumulatesPerBowl();
        Test_TotalFinalScore_ResetToZero();
        Test_StartCurrentPot_ResetsTotalFinalScore();
        Test_FinalPot_TotalFinalScore_EqualsFinalScore();

        // B1：新满意条件
        Test_PotTotalScoreAtLeast_SatisfiedAndNotSatisfied();
        Test_PotTotalScoreAtLeast_ThresholdZero_AlwaysSatisfied();

        // B1：饕餮定义与配置
        Test_TaotieDefinitions_MatchBossConfig();
        Test_TaotieForms_GetTaotieDefinition();
        Test_BossConfig_GetChapterForm_AndOutOfRange();

        // B1：仙丹粉末与效果
        Test_ImmortalPowder_NotInNormalRegistry();
        Test_ImmortalPowder_Effects();
        Test_AddAllFlavorsEffect_AppliesToAllSeven();

        // B2：章末接入
        Test_ChapterBoss_AssignOnlyAtChapterLastPotBowl10();
        Test_ChapterBoss_TriggersOncePerChapter_InOrder();
        Test_ChapterBoss_Satisfied_GrantsOnePowder_AndCanAdvance();
        Test_ChapterBoss_TwoChaptersSatisfied_KeepsAtMostOnePowder();
        Test_ChapterBoss_Disliked_FailsRunAndSuppressesOutputs();
        Test_ChapterBoss_Satisfied_NotInCompanionCandidates();
        Test_ChapterBoss_RecordMatchesRunState();
        Test_ChapterBoss_Disliked_RecordScoreBelowThreshold();
        Test_RunController_StartRun_ClearsFailState_AndFailRunGuards();

        Console.WriteLine("All BossSettlementTests passed.");
    }

    // ── B3 待补用例（本阶段刻意不写，避免未实现即红）─────────────────────────
    //  - 最终锅走真身、结算后本局结束并记录结局（B3）。

    // ── 辅助 ─────────────────────────────────────────────────────────────────

    /// <summary>推进 Run 经过全部 9 锅普通锅，返回处于 IsFinalPot=true 状态的 RunController。</summary>
    static RunController MakeRunAtFinalPot(GameState state)
    {
        var run = new RunController(state);
        run.StartRun();
        int total = RunController.ChaptersPerRun * RunController.PotsPerChapter;
        for (int i = 0; i < total; i++)
        {
            var ctrl = run.StartCurrentPot();
            RunNormalPotToEnd(ctrl);
            ctrl.ClosePot();
            run.AdvanceToNextPot();
        }
        Assert(state.Run.IsFinalPot, "MakeRunAtFinalPot: 应已进入 Final Pot");
        return run;
    }

    /// <summary>快速跑完一整锅（10 碗），不调用 ClosePot。</summary>
    static void RunNormalPotToEnd(PotController ctrl)
    {
        for (int bowl = 1; bowl <= 10; bowl++)
        {
            ctrl.StartBowl();
            for (int s = 0; s < 9; s++) ctrl.AdvanceBowlPhase();
            if (bowl < 10) ctrl.StartNextBowl();
        }
    }

    static CustomerInstance MakeNormalCustomer() =>
        new(new CustomerDefinition("c_normal", "普通食客", isRare: false));

    static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception($"[FAIL] BossSettlementTests: {message}");
    }

    // ── B0：MetaState ────────────────────────────────────────────────────────

    /// <summary>MetaState 最多保留 1 个仙丹粉末；消耗后归零，空时 Consume 返回 null。</summary>
    static void Test_MetaState_KeepsAtMostOne()
    {
        var meta = new MetaState();
        var first = ItemData.CreateImmortalPowder();
        var second = ItemData.CreateImmortalPowder();

        Assert(meta.ImmortalPowderCount == 0, "初始仙丹粉末数量应为 0");
        Assert(meta.TryAddImmortalPowder(first), "第 1 次 TryAdd 应返回 true");
        Assert(!meta.TryAddImmortalPowder(second), "第 2 次 TryAdd 应返回 false（已达上限）");
        Assert(meta.ImmortalPowderCount == 1, "超过上限后数量仍应为 1");
        Assert(ReferenceEquals(meta.ImmortalPowders[0], first), "保留的应是第 1 个实例");

        var consumed = meta.ConsumeImmortalPowder();
        Assert(ReferenceEquals(consumed, first), "Consume 应取出已保留的实例");
        Assert(meta.ImmortalPowderCount == 0, "Consume 后数量应归零");
        Assert(meta.ConsumeImmortalPowder() == null, "空容器 Consume 应返回 null");
    }

    /// <summary>TryAddImmortalPowder(null) 应抛 ArgumentNullException。</summary>
    static void Test_MetaState_NullThrows()
    {
        var meta = new MetaState();
        bool threw = false;
        try { meta.TryAddImmortalPowder(null!); }
        catch (ArgumentNullException) { threw = true; }
        Assert(threw, "传入 null 应抛 ArgumentNullException");
    }

    /// <summary>GameController 注入的 MetaState 被原样持有；未注入时自建一个。</summary>
    static void Test_GameController_MetaState_Injection()
    {
        var meta = new MetaState();
        var injected = new GameController(metaState: meta);
        Assert(ReferenceEquals(injected.Meta, meta), "注入的 MetaState 应被 GameController 原样持有");

        var defaulted = new GameController();
        Assert(defaulted.Meta != null, "未注入时 GameController 应自建 MetaState");
    }

    // ── B1：TotalFinalScore ──────────────────────────────────────────────────

    /// <summary>TotalFinalScore 累加的是含倍率的 FinalScore（不是 BaseScore）。</summary>
    static void Test_TotalFinalScore_AccumulatesFinalScoreWithMultiplier()
    {
        var pot = new PotState { BowlNumber = 6, BaseScore = 1 };
        ScoreCalculator.CalculateAndLock(pot);
        Assert(pot.FinalScore == 2, "第 6 碗 BaseScore=1 应按 ×2 得 FinalScore=2（含倍率）");
        Assert(pot.TotalFinalScore == 2, "首次锁分后累计应为 2（含倍率，而非 BaseScore=1）");

        // 模拟下一碗：普通锅 StartBowl 会重置碗内分数，这里手工复位以复用同一 PotState。
        pot.IsScoreLocked = false;
        pot.BowlNumber = 7;
        pot.BaseScore = 1;
        ScoreCalculator.CalculateAndLock(pot);
        Assert(pot.FinalScore == 4, "第 7 碗 BaseScore=1 应按 ×4 得 FinalScore=4");
        Assert(pot.TotalFinalScore == 6, $"累计应为 2+4=6（含倍率），实际 {pot.TotalFinalScore}");
    }

    /// <summary>
    /// 端到端累计：普通锅逐碗走 StartBowl() → 设分 → CalculateScore()，
    /// 断言每碗 FinalScore 与累计 TotalFinalScore 相等，覆盖第 6/7 碗的 ×2/×4 档位，
    /// 并证明 StartBowl() 不会清零已累计的 TotalFinalScore。
    /// </summary>
    static void Test_TotalFinalScore_EndToEndAccumulatesPerBowl()
    {
        var state = new GameState();
        var run = new RunController(state);
        run.StartRun();
        var ctrl = run.StartCurrentPot();

        int expectedTotal = 0;
        for (int bowl = 1; bowl <= 10; bowl++)
        {
            ctrl.StartBowl();
            state.Pot.BaseScore = bowl; // 设分：第 N 碗基础分 N；无味道，倍率仅取碗数档

            // Start → ScoreCalculation 需推进 5 次
            for (int i = 0; i < 5; i++) ctrl.AdvanceBowlPhase();
            ctrl.CalculateScore();

            int expectedFinal = bowl * ScoreCalculator.GetMultiplier(bowl);
            Assert(state.Pot.FinalScore == expectedFinal,
                $"第 {bowl} 碗 FinalScore 应为 {expectedFinal}（BaseScore={bowl}×{ScoreCalculator.GetMultiplier(bowl)}），实际 {state.Pot.FinalScore}");

            expectedTotal += expectedFinal;
            Assert(state.Pot.TotalFinalScore == expectedTotal,
                $"第 {bowl} 碗后 TotalFinalScore 应为 {expectedTotal}，实际 {state.Pot.TotalFinalScore}（StartBowl 不应清零累计）");

            if (bowl < 10)
            {
                for (int i = 0; i < 4; i++) ctrl.AdvanceBowlPhase(); // ScoreCalculation → End
                ctrl.StartNextBowl();
            }
        }

        Assert(expectedTotal == 583, $"各碗 FinalScore 之和应为 583，实际 {expectedTotal}");
    }

    /// <summary>PotState.Reset() 应把 TotalFinalScore 归零。</summary>
    static void Test_TotalFinalScore_ResetToZero()
    {
        var pot = new PotState { BowlNumber = 6, BaseScore = 1 };
        ScoreCalculator.CalculateAndLock(pot);
        Assert(pot.TotalFinalScore == 2, "前置：累计应为 2");

        pot.Reset(10);
        Assert(pot.TotalFinalScore == 0, "Reset() 后 TotalFinalScore 应归零");
    }

    /// <summary>RunController.StartCurrentPot() 内部 Reset 锅状态，TotalFinalScore 归零。</summary>
    static void Test_StartCurrentPot_ResetsTotalFinalScore()
    {
        var state = new GameState();
        var run = new RunController(state);
        run.StartRun();

        state.Pot.TotalFinalScore = 99;
        run.StartCurrentPot();

        Assert(state.Pot.TotalFinalScore == 0, "StartCurrentPot 后 TotalFinalScore 应归零");
    }

    /// <summary>最终锅一次性结算后 TotalFinalScore == FinalScore。</summary>
    static void Test_FinalPot_TotalFinalScore_EqualsFinalScore()
    {
        var state = new GameState();
        var run = MakeRunAtFinalPot(state);
        run.StartCurrentPot();

        state.Pot.BaseScore = 10;
        run.EndCooking(MakeNormalCustomer());

        Assert(state.Pot.FinalScore == 320, "最终锅 BaseScore=10 应按 ×32 得 320");
        Assert(state.Pot.TotalFinalScore == state.Pot.FinalScore,
            "最终锅结算后 TotalFinalScore 应等于 FinalScore");
    }

    // ── B1：新满意条件 ───────────────────────────────────────────────────────

    /// <summary>PotTotalScoreAtLeast：累计达标满意、不达标不满意。</summary>
    static void Test_PotTotalScoreAtLeast_SatisfiedAndNotSatisfied()
    {
        var definition = new CustomerDefinition(
            id: "score_boss_test",
            name: "测试饕餮",
            isRare: true,
            satisfactionConditions: new[]
            {
                new CustomerSatisfactionCondition(ConditionType.PotTotalScoreAtLeast, 10)
            });

        var pot = new PotState { TotalFinalScore = 10 };
        Assert(SatisfactionEvaluator.IsSatisfied(definition, pot),
            "累计 == 阈值应判定满意");

        pot.TotalFinalScore = 9;
        Assert(!SatisfactionEvaluator.IsSatisfied(definition, pot),
            "累计 < 阈值应判定不满意");

        pot.TotalFinalScore = 11;
        Assert(SatisfactionEvaluator.IsSatisfied(definition, pot),
            "累计 > 阈值应判定满意");
    }

    /// <summary>PotTotalScoreAtLeast 阈值 = 0 时，任何非负累计分都应恒满意。</summary>
    static void Test_PotTotalScoreAtLeast_ThresholdZero_AlwaysSatisfied()
    {
        var definition = new CustomerDefinition(
            id: "zero_threshold_boss_test",
            name: "零阈值测试饕餮",
            isRare: true,
            satisfactionConditions: new[]
            {
                new CustomerSatisfactionCondition(ConditionType.PotTotalScoreAtLeast, 0)
            });

        var pot = new PotState { TotalFinalScore = 0 };
        Assert(SatisfactionEvaluator.IsSatisfied(definition, pot),
            "阈值 0：累计 == 0 应满意");

        pot.TotalFinalScore = 1;
        Assert(SatisfactionEvaluator.IsSatisfied(definition, pot),
            "阈值 0：累计 > 0 应满意");

        pot.TotalFinalScore = 9999;
        Assert(SatisfactionEvaluator.IsSatisfied(definition, pot),
            "阈值 0：任意正累计分仍应满意");
    }

    // ── B1：饕餮定义与配置 ───────────────────────────────────────────────────

    /// <summary>4 个饕餮定义与 BossConfig 完全一致，且不绑伙伴、条件为累计分。</summary>
    static void Test_TaotieDefinitions_MatchBossConfig()
    {
        var config = BossConfig.Default;

        AssertTaotie(CustomerData.TaotieChild, config.ChapterForms[0]);
        AssertTaotie(CustomerData.TaotieMaiden, config.ChapterForms[1]);
        AssertTaotie(CustomerData.TaotieLady, config.ChapterForms[2]);
        AssertTaotie(CustomerData.TaotieTrue, config.TrueForm);

        Assert(CustomerData.TaotieForms.Count == 4, "TaotieForms 应包含 4 个形态");
        Assert(CustomerData.TaotieForms[0].Id == "taotie_child", "TaotieForms[0] 应为 taotie_child");
        Assert(CustomerData.TaotieForms[3].Id == "taotie_true", "TaotieForms[3] 应为 taotie_true");
    }

    static void AssertTaotie(CustomerDefinition definition, BossFormConfig form)
    {
        Assert(definition.Id == form.Id, $"Id 应等于 BossConfig：期望 {form.Id}，实际 {definition.Id}");
        Assert(definition.Name == form.Name, $"Name 应等于 BossConfig：期望 {form.Name}，实际 {definition.Name}");
        Assert(definition.IsRare, $"{form.Id} 应为稀有食客");
        Assert(definition.CompanionReward == null, $"{form.Id} 不应绑定伙伴（CompanionReward 应为 null）");
        Assert(definition.SatisfactionConditions.Count == 1, $"{form.Id} 应恰好有 1 个满意条件");
        Assert(definition.SatisfactionConditions[0].ConditionType == ConditionType.PotTotalScoreAtLeast,
            $"{form.Id} 的满意条件应为 PotTotalScoreAtLeast");
        Assert(definition.SatisfactionConditions[0].Threshold == form.SatisfyThreshold,
            $"{form.Id} 的阈值应等于 BossConfig：期望 {form.SatisfyThreshold}");
    }

    /// <summary>GetTaotieDefinition 命中与未命中；工厂方法产出的实例定义正确。</summary>
    static void Test_TaotieForms_GetTaotieDefinition()
    {
        Assert(CustomerData.GetTaotieDefinition("taotie_maiden").Id == "taotie_maiden",
            "GetTaotieDefinition 应能按 Id 命中");

        bool threw = false;
        try { CustomerData.GetTaotieDefinition("no_such_form"); }
        catch (ArgumentException) { threw = true; }
        Assert(threw, "未知形态 Id 应抛 ArgumentException");

        Assert(ReferenceEquals(CustomerData.CreateTaotieInstance("taotie_lady").Definition,
                CustomerData.TaotieLady),
            "CreateTaotieInstance 应使用对应 Definition");
        Assert(ReferenceEquals(CustomerData.CreateTaotieTrueInstance().Definition, CustomerData.TaotieTrue),
            "CreateTaotieTrueInstance 应使用真身 Definition");
    }

    /// <summary>GetChapterForm(1/2/3) 映射正确，越界抛异常；GetForm 未命中抛异常。</summary>
    static void Test_BossConfig_GetChapterForm_AndOutOfRange()
    {
        var config = BossConfig.Default;

        Assert(config.GetChapterForm(1).Id == "taotie_child", "第 1 章末应为 taotie_child");
        Assert(config.GetChapterForm(2).Id == "taotie_maiden", "第 2 章末应为 taotie_maiden");
        Assert(config.GetChapterForm(3).Id == "taotie_lady", "第 3 章末应为 taotie_lady");
        Assert(config.GetForm("taotie_true").Id == "taotie_true", "GetForm 应能取到真身");

        bool threw = false;
        try { config.GetChapterForm(0); }
        catch (ArgumentOutOfRangeException) { threw = true; }
        Assert(threw, "GetChapterForm(0) 应抛 ArgumentOutOfRangeException");

        threw = false;
        try { config.GetChapterForm(4); }
        catch (ArgumentOutOfRangeException) { threw = true; }
        Assert(threw, "GetChapterForm(4) 应抛 ArgumentOutOfRangeException");

        threw = false;
        try { config.GetForm("no_such_form"); }
        catch (ArgumentException) { threw = true; }
        Assert(threw, "GetForm 未知 Id 应抛 ArgumentException");
    }

    // ── B1：仙丹粉末 ─────────────────────────────────────────────────────────

    /// <summary>仙丹粉末不在随机池 / 商店池（Registry 仍只有 5 个），只在 SpecialRegistry。</summary>
    static void Test_ImmortalPowder_NotInNormalRegistry()
    {
        Assert(ItemData.Registry.GetAll().Count == 5, "正式 Registry 仍应只有原 5 个道具");
        Assert(!ItemData.Registry.GetAll().Any(d => d.Id == "immortal_powder"),
            "正式 Registry 不应包含仙丹粉末");

        var rng = new Random(12345);
        var randoms = ItemData.CreateRandomInstances(100, rng);
        Assert(!randoms.Any(i => i.Definition.Id == "immortal_powder"),
            "随机抽取不应出现仙丹粉末");

        Assert(ItemData.SpecialRegistry.Get("immortal_powder").Id == "immortal_powder",
            "SpecialRegistry 应能取到仙丹粉末");
    }

    /// <summary>
    /// 仙丹粉末效果：基础分 +N，且七味全部 +M，经真实路径 PotController.ApplyItemEffect 验证
    /// （效果链 = EffectSystem.TriggerAll，同一 sourceId 的多个效果全部生效）。
    /// </summary>
    static void Test_ImmortalPowder_Effects()
    {
        var state = new GameState();
        var run = new RunController(state);
        run.StartRun();
        var ctrl = run.StartCurrentPot();
        ctrl.StartBowl();
        ctrl.AdvanceBowlPhase(); // Start → Customer
        ctrl.AdvanceBowlPhase(); // Customer → ItemPhase（合法用道具时机）

        var powder = ItemData.CreateImmortalPowder();
        ctrl.ApplyItemEffect(powder, new EffectSystem());

        Assert(state.Pot.BaseScore == BossConfig.Default.ImmortalPowderBaseScore,
            $"仙丹粉末应给基础分 +{BossConfig.Default.ImmortalPowderBaseScore}，实际 {state.Pot.BaseScore}");

        foreach (FlavorType flavor in Enum.GetValues<FlavorType>())
        {
            Assert(state.Pot.GetFlavor(flavor) == BossConfig.Default.ImmortalPowderFlavorAmount,
                $"仙丹粉末应给 {flavor} +{BossConfig.Default.ImmortalPowderFlavorAmount}，实际 {state.Pot.GetFlavor(flavor)}");
        }
    }

    /// <summary>AddAllFlavorsEffect 对全部 7 种味道各 +N。</summary>
    static void Test_AddAllFlavorsEffect_AppliesToAllSeven()
    {
        var effect = new AddAllFlavorsEffect(3, effectId: "test_all_flavors");
        var pot = new PotState();
        var context = new EffectContext(pot.BowlNumber, new GameState(), pot);

        effect.Apply(context);

        Assert(Enum.GetValues<FlavorType>().Length == 7, "FlavorType 应恰好有 7 种");
        foreach (FlavorType flavor in Enum.GetValues<FlavorType>())
            Assert(pot.GetFlavor(flavor) == 3, $"{flavor} 应 +3，实际 {pot.GetFlavor(flavor)}");
    }

    // ── B2：章末接入 辅助 ────────────────────────────────────────────────────

    /// <summary>默认关闭稀有食客，避免具名稀有食客带来的伙伴候选干扰。</summary>
    static CustomerAppearanceConfig NoRare() =>
        new() { RareBowlNumbers = Array.Empty<int>() };

    static bool IsRareCustomerId(string id) =>
        id is "sweet_boss_customer" or "generous_guest_customer";

    /// <summary>
    /// B2 测试脚手架：不调用 StartNewGame（保持空食材篮），直接把 GameController 置于
    /// 「已 StartRun」的等价初始状态，订阅 Boss 相关事件。
    /// </summary>
    private sealed class BossHarness
    {
        public GameState State { get; } = new();
        public MetaState Meta { get; } = new();
        public BossConfig Boss { get; } = BossConfig.Default;
        public GameController Gc { get; }
        public List<BossEncounteredEvent> Encounters { get; } = new();
        public List<BossEvaluatedEvent> Evaluations { get; } = new();
        public List<RunFailedEvent> Failures { get; } = new();
        public List<CustomerServedEvent> Servings { get; } = new();

        public BossHarness(
            int seed,
            PotRewardConfig? potReward = null,
            CustomerAppearanceConfig? appearance = null)
        {
            // 阈值统一读 BossConfig.Default（与生产代码同源），不再注入配置。
            Gc = new GameController(
                State,
                appearance ?? NoRare(),
                new Random(seed),
                potReward,
                metaState: Meta);
            Gc.Events.Subscribe<BossEncounteredEvent>(Encounters.Add);
            Gc.Events.Subscribe<BossEvaluatedEvent>(Evaluations.Add);
            Gc.Events.Subscribe<RunFailedEvent>(Failures.Add);
            Gc.Events.Subscribe<CustomerServedEvent>(Servings.Add);

            // 不调用 StartNewGame（保持空食材篮），直接启动第 1 锅；
            // RunState 默认即 (Chapter=1, PotIndex=1)，无需 StartRun。
            Gc.StartCurrentPot();
        }
    }

    /// <summary>
    /// 逐碗走完当前锅（空食材篮 → 候选为空 → SkipBowl），返回每碗食客 Id（按碗序）。
    /// 本锅最后一碗（<see cref="PotState.BowlLimit"/>）按 <paramref name="satisfyBoss"/> 设定分数。
    /// </summary>
    static List<string> WalkPot(BossHarness h, bool satisfyBoss)
    {
        var ids = new List<string>();
        int guard = 0;
        while (h.Gc.Pot.Phase == PotPhase.InProgress && guard++ < 500)
        {
            ids.Add(h.Gc.CurrentCustomer?.Definition.Id ?? "<null>");

            if (h.Gc.CanSelectIngredient)
            {
                h.Gc.SelectIngredient(h.Gc.CurrentCandidates[0].InstanceId);
                continue;
            }

            if (!h.Gc.CanSkipBowl)
                throw new Exception(
                    $"[FAIL] BossSettlementTests: 卡在第 {h.Gc.Pot.BowlNumber} 碗（{h.Gc.Pot.CurrentBowlPhase}）");

            if (h.Gc.Pot.BowlNumber == h.Gc.Pot.BowlLimit)
                h.Gc.Pot.BaseScore = satisfyBoss
                    ? h.Boss.GetChapterForm(h.Gc.Run.Chapter).SatisfyThreshold
                    : 0;

            h.Gc.SkipBowl();
        }

        Assert(h.Gc.Pot.Phase == PotPhase.Ended, "WalkPot：锅应已 Ended");
        return ids;
    }

    /// <summary>走完锅的收尾三道环节：奖励 → 伙伴 → 商店。</summary>
    static void SettlePot(BossHarness h)
    {
        if (h.Gc.CanChooseReward)
            h.Gc.ChooseReward(h.Gc.RewardCandidates[0].InstanceId);
        if (h.Gc.CanSkipCompanionChoice)
            h.Gc.SkipCompanionChoice();
        if (h.Gc.IsShopOpen)
            h.Gc.SkipShop();
    }

    // ── B2：章末指派 ─────────────────────────────────────────────────────────

    /// <summary>
    /// 仅「本章最后一锅（第 3 锅）第 10 碗」指派对应形态饕餮；
    /// 第 1/2 锅第 10 碗与第 9 碗仍是普通食客；第 5/7 碗稀有行为不变。
    /// </summary>
    static void Test_ChapterBoss_AssignOnlyAtChapterLastPotBowl10()
    {
        var h = new BossHarness(
            seed: 9001,
            potReward: new PotRewardConfig { ChoiceCount = 0 },
            appearance: new CustomerAppearanceConfig()); // 默认：第 5/7 碗稀有

        var pot1 = WalkPot(h, satisfyBoss: true);
        Assert(h.Encounters.Count == 0, "第 1 锅不应触发章末 Boss");
        Assert(pot1[8] == "normal_customer", $"第 1 锅第 9 碗应为普通食客，实际 {pot1[8]}");
        Assert(pot1[9] == "normal_customer", $"第 1 锅第 10 碗应为普通食客，实际 {pot1[9]}");
        Assert(IsRareCustomerId(pot1[4]), $"第 1 锅第 5 碗应仍是稀有食客，实际 {pot1[4]}");
        Assert(IsRareCustomerId(pot1[6]), $"第 1 锅第 7 碗应仍是稀有食客，实际 {pot1[6]}");
        SettlePot(h);
        h.Gc.AdvanceToNextPot();

        var pot2 = WalkPot(h, satisfyBoss: true);
        Assert(h.Encounters.Count == 0, "第 2 锅不应触发章末 Boss");
        Assert(pot2[9] == "normal_customer", $"第 2 锅第 10 碗应为普通食客，实际 {pot2[9]}");
        SettlePot(h);
        h.Gc.AdvanceToNextPot();

        var pot3 = WalkPot(h, satisfyBoss: true);
        Assert(h.Encounters.Count == 1, $"第 3 锅应触发一次章末 Boss，实际 {h.Encounters.Count}");
        Assert(pot3[8] == "normal_customer", $"第 3 锅第 9 碗应为普通食客，实际 {pot3[8]}");
        Assert(pot3[9] == "taotie_child", $"第 3 锅第 10 碗应为 taotie_child，实际 {pot3[9]}");
        Assert(h.Encounters[0].BowlNumber == 10, "Boss 应出现在第 10 碗");
    }

    /// <summary>跑完整 9 锅：跨章各触发一次，顺序 child/maiden/lady，位置 (1,3)/(2,3)/(3,3)，无重复。</summary>
    static void Test_ChapterBoss_TriggersOncePerChapter_InOrder()
    {
        var h = new BossHarness(seed: 9002, potReward: new PotRewardConfig { ChoiceCount = 0 });

        for (int pot = 1; pot <= 9; pot++)
        {
            WalkPot(h, satisfyBoss: true);
            Assert(!h.Gc.Run.IsFailed, $"第 {pot} 锅满意后本局不应失败");
            SettlePot(h);
            if (pot < 9)
                h.Gc.AdvanceToNextPot();
        }

        Assert(h.Encounters.Count == 3, $"跨 3 章应恰好触发 3 次 Boss，实际 {h.Encounters.Count}");
        Assert(h.Encounters[0].BossId == "taotie_child"
            && h.Encounters[0].Chapter == 1
            && h.Encounters[0].PotIndex == 3
            && !h.Encounters[0].IsFinalPot,
            "第 1 次应为 (1,3) taotie_child");
        Assert(h.Encounters[1].BossId == "taotie_maiden"
            && h.Encounters[1].Chapter == 2
            && h.Encounters[1].PotIndex == 3,
            "第 2 次应为 (2,3) taotie_maiden");
        Assert(h.Encounters[2].BossId == "taotie_lady"
            && h.Encounters[2].Chapter == 3
            && h.Encounters[2].PotIndex == 3,
            "第 3 次应为 (3,3) taotie_lady");
        Assert(h.Encounters.All(e => e.BowlNumber == 10), "三次 Boss 均应出现在第 10 碗");
    }

    /// <summary>满意：发下 1 个仙丹粉末、不付金币 / 不给随机掉落、可推进下一锅。</summary>
    static void Test_ChapterBoss_Satisfied_GrantsOnePowder_AndCanAdvance()
    {
        var h = new BossHarness(seed: 9003, potReward: new PotRewardConfig { ChoiceCount = 0 });

        WalkPot(h, true); SettlePot(h); h.Gc.AdvanceToNextPot();
        WalkPot(h, true); SettlePot(h); h.Gc.AdvanceToNextPot();

        int basketBefore = h.State.Player.IngredientBasket.Count;
        int itemsBefore = h.State.Player.Items.Count;

        var ids = WalkPot(h, satisfyBoss: true);
        Assert(ids[9] == "taotie_child", "第 3 锅第 10 碗应为饕餮·幼体");

        var bossServing = h.Servings.Single(s => s.Customer.Definition.Id == "taotie_child");
        Assert(bossServing.GoldAwarded == 0, "饕餮不付金币");
        Assert(h.State.Player.IngredientBasket.Count == basketBefore, "饕餮不给随机食材掉落");
        Assert(h.State.Player.Items.Count == itemsBefore, "饕餮不给随机道具掉落");

        var eval = h.Evaluations.Single();
        Assert(eval.Satisfied, "饕餮应判定满意");
        Assert(eval.RewardGranted, "首次满意应真正发下赏赐");
        Assert(eval.PotTotalFinalScore >= eval.Threshold, "累计最终分应达标");

        // 钉住满意路径的计分事实：第 10 碗取 ×32 档，前 9 碗贡献 0（累计 == 第 10 碗最终分）。
        var record = h.Gc.Run.ChapterBossRecords.Single();
        int effectiveMultiplier = ScoreCalculator.GetEffectiveMultiplier(h.Gc.Pot);
        Assert(effectiveMultiplier == 32,
            $"章末在第 10 碗，生效倍率应为 ×32，实际 ×{effectiveMultiplier}");
        Assert(record.PotTotalFinalScore == h.Gc.Pot.FinalScore,
            $"前 9 碗贡献 0，累计最终分应等于第 10 碗 FinalScore（{h.Gc.Pot.FinalScore}），实际 {record.PotTotalFinalScore}");
        Assert(record.Threshold == BossConfig.Default.GetChapterForm(h.Gc.Run.Chapter).SatisfyThreshold,
            "记录阈值应来自 BossConfig.Default 的本章形态");

        Assert(!h.Gc.Run.IsFailed, "满意不应终止本局");
        Assert(h.Meta.ImmortalPowderCount == 1, "跨局容器应保留 1 个仙丹粉末");
        Assert(h.Failures.Count == 0, "满意不应发布 RunFailedEvent");

        SettlePot(h);
        Assert(h.Gc.CanAdvanceToNextPot, "满意后可推进下一锅");
        h.Gc.AdvanceToNextPot();
        Assert(h.Gc.Run.Chapter == 2 && h.Gc.Run.PotIndex == 1,
            $"推进后应进入 (2,1)，实际 ({h.Gc.Run.Chapter},{h.Gc.Run.PotIndex})");
    }

    /// <summary>连续两章满意：跨局仍只保留 1 个，第二次 RewardGranted == false。</summary>
    static void Test_ChapterBoss_TwoChaptersSatisfied_KeepsAtMostOnePowder()
    {
        var h = new BossHarness(seed: 9004, potReward: new PotRewardConfig { ChoiceCount = 0 });

        for (int pot = 1; pot <= 6; pot++)
        {
            WalkPot(h, true); SettlePot(h);
            if (pot < 6)
                h.Gc.AdvanceToNextPot();
        }

        Assert(h.Evaluations.Count == 2, $"应触发 2 次 Boss 判定，实际 {h.Evaluations.Count}");
        Assert(h.Evaluations[0].Satisfied && h.Evaluations[0].RewardGranted,
            "第 1 章末满意应发下赏赐");
        Assert(h.Evaluations[1].Satisfied && !h.Evaluations[1].RewardGranted,
            "第 2 章末满意但跨局上限已满，RewardGranted 应为 false");
        Assert(h.Meta.ImmortalPowderCount == 1, "跨局仍只保留 1 个仙丹粉末");
    }

    /// <summary>嫌弃：本局终止、发布事件、不可推进、不再产出奖励 / 伙伴 / 商店。</summary>
    static void Test_ChapterBoss_Disliked_FailsRunAndSuppressesOutputs()
    {
        // 默认奖励（3 选 1）与商店：作为「正常锅会产出」的对照。
        var h = new BossHarness(seed: 9005);

        WalkPot(h, true);
        Assert(h.Gc.ShopOffers.Count > 0, "对照：正常锅结束应产出商店报价");
        SettlePot(h);
        h.State.Player.IngredientBasket.Clear();
        h.Gc.AdvanceToNextPot();

        WalkPot(h, true);
        SettlePot(h);
        h.State.Player.IngredientBasket.Clear();
        h.Gc.AdvanceToNextPot();

        var ids = WalkPot(h, satisfyBoss: false);
        Assert(ids[9] == "taotie_child", "第 3 锅第 10 碗应为饕餮·幼体（嫌弃）");

        Assert(h.Gc.Run.IsFailed, "嫌弃后本局应终止");
        Assert(!string.IsNullOrWhiteSpace(h.Gc.Run.FailReason), "FailReason 应非空");
        Assert(h.Failures.Count == 1, $"应恰好发布一次 RunFailedEvent，实际 {h.Failures.Count}");
        Assert(h.Failures[0].Reason == h.Gc.Run.FailReason, "RunFailedEvent.Reason 应与 FailReason 一致");
        Assert(h.Failures[0].Chapter == 1 && h.Failures[0].PotIndex == 3, "RunFailedEvent 应指明 (1,3)");
        Assert(h.Meta.ImmortalPowderCount == 0, "嫌弃不应发放仙丹粉末");

        var eval = h.Evaluations.Single();
        Assert(!eval.Satisfied, "应判定为嫌弃");
        Assert(!eval.RewardGranted, "嫌弃不应发赏赐");

        // 失败收口：不再产出奖励 / 伙伴 / 商店。
        Assert(h.Gc.RewardCandidates.Count == 0, "失败锅不应产出奖励候选");
        Assert(h.Gc.CompanionCandidates.Count == 0, "失败锅不应产出伙伴候选");
        Assert(h.Gc.ShopOffers.Count == 0, "失败锅不应产出商店报价");
        Assert(!h.Gc.IsAwaitingReward, "失败锅不应处于等待奖励");
        Assert(!h.Gc.IsAwaitingCompanionChoice, "失败锅不应处于等待伙伴");
        Assert(!h.Gc.IsShopOpen, "失败锅商店不应营业");

        Assert(!h.Gc.CanAdvanceToNextPot, "失败后不可推进下一锅");
        bool threw = false;
        try { h.Gc.AdvanceToNextPot(); }
        catch (InvalidOperationException) { threw = true; }
        Assert(threw, "失败后 AdvanceToNextPot 应抛 InvalidOperationException");
        Assert(h.Gc.IsRunComplete, "失败后 IsRunComplete 应为 true");
    }

    /// <summary>满意的饕餮不进入伙伴候选（未绑定伙伴）。</summary>
    static void Test_ChapterBoss_Satisfied_NotInCompanionCandidates()
    {
        var h = new BossHarness(seed: 9006, potReward: new PotRewardConfig { ChoiceCount = 0 });

        WalkPot(h, true); SettlePot(h); h.Gc.AdvanceToNextPot();
        WalkPot(h, true); SettlePot(h); h.Gc.AdvanceToNextPot();
        WalkPot(h, satisfyBoss: true);

        Assert(h.Gc.Run.ChapterBossRecords.Single().Satisfied, "前置：章末 Boss 应满意");
        Assert(h.State.Customer.SatisfiedRareCustomers.Any(c => c.Definition.Id == "taotie_child"),
            "前置：满意的饕餮应被记录为满意稀有食客");
        Assert(h.Gc.CompanionCandidates.Count == 0,
            "满意的饕餮不绑定伙伴，不应产生伙伴候选");
        Assert(!h.Gc.CompanionCandidates.Any(c => c.Id.Contains("taotie")),
            "伙伴候选中不应出现饕餮对应项");
    }

    /// <summary>ChapterBossRecords 记录内容正确，且跨章追加保留顺序。</summary>
    static void Test_ChapterBoss_RecordMatchesRunState()
    {
        var h = new BossHarness(seed: 9007, potReward: new PotRewardConfig { ChoiceCount = 0 });

        WalkPot(h, true); SettlePot(h); h.Gc.AdvanceToNextPot();
        WalkPot(h, true); SettlePot(h); h.Gc.AdvanceToNextPot();
        WalkPot(h, true);

        var records = h.Gc.Run.ChapterBossRecords;
        Assert(records.Count == 1, $"应记录一条章末 Boss 验收，实际 {records.Count}");

        var form = h.Boss.GetChapterForm(1);
        var record = records[0];
        Assert(record.Chapter == 1, "记录章应为 1");
        Assert(record.PotIndex == 3, "记录锅应为 3");
        Assert(record.BossId == "taotie_child", $"记录 BossId 应为 taotie_child，实际 {record.BossId}");
        Assert(record.BossName == form.Name, "记录 BossName 应与配置一致");
        Assert(record.Satisfied, "记录应为满意");
        Assert(record.Threshold == form.SatisfyThreshold, "记录阈值应与配置一致");
        Assert(record.PotTotalFinalScore == h.Gc.Pot.TotalFinalScore,
            "记录的本锅累计最终分应等于结算时的 Pot.TotalFinalScore");
        Assert(record.PotTotalFinalScore >= record.Threshold, "累计最终分应达标");

        SettlePot(h);
        h.Gc.AdvanceToNextPot();
        for (int pot = 4; pot <= 6; pot++)
        {
            WalkPot(h, true); SettlePot(h);
            if (pot < 6)
                h.Gc.AdvanceToNextPot();
        }

        Assert(h.Gc.Run.ChapterBossRecords.Count == 2, "跨章后应追加第 2 条记录");
        Assert(h.Gc.Run.ChapterBossRecords[1].Chapter == 2, "第 2 条记录应为第 2 章");
        Assert(h.Gc.Run.ChapterBossRecords[1].BossId == "taotie_maiden",
            "第 2 条记录应为 taotie_maiden");
    }

    /// <summary>StartRun 清理失败态与记录；FailRun 的空白 / 重复校验。</summary>
    static void Test_RunController_StartRun_ClearsFailState_AndFailRunGuards()
    {
        var state = new GameState();
        var run = new RunController(state);
        run.StartRun();

        state.Run.IsFailed = true;
        state.Run.FailReason = "旧原因";
        state.Run.ChapterBossRecords.Add(
            new ChapterBossRecord(1, 3, "taotie_child", "饕餮·幼体", true, 5, 30));

        run.StartRun();
        Assert(!state.Run.IsFailed, "StartRun 应清空 IsFailed");
        Assert(state.Run.FailReason == null, "StartRun 应清空 FailReason");
        Assert(state.Run.ChapterBossRecords.Count == 0, "StartRun 应清空 ChapterBossRecords");

        bool threw = false;
        try { run.FailRun("   "); }
        catch (ArgumentException) { threw = true; }
        Assert(threw, "FailRun 空白 reason 应抛 ArgumentException");

        run.FailRun("嫌弃，但是鼓励");
        Assert(state.Run.IsFailed && state.Run.FailReason == "嫌弃，但是鼓励",
            "FailRun 应置位 IsFailed / FailReason");

        threw = false;
        try { run.FailRun("再次终止"); }
        catch (InvalidOperationException) { threw = true; }
        Assert(threw, "重复 FailRun 应抛 InvalidOperationException");

        threw = false;
        try { run.AdvanceToNextPot(); }
        catch (InvalidOperationException) { threw = true; }
        Assert(threw, "失败后 AdvanceToNextPot 应抛 InvalidOperationException");

        threw = false;
        try { run.EndCooking(MakeNormalCustomer()); }
        catch (InvalidOperationException) { threw = true; }
        Assert(threw, "失败后 EndCooking 应抛 InvalidOperationException");

        Assert(run.IsRunComplete, "失败后 IsRunComplete 应为 true");
    }

    /// <summary>第 1 章第 3 锅第 10 碗（空锅嫌弃）的累计最终分应为 0，阈值来自配置。</summary>
    static void Test_ChapterBoss_Disliked_RecordScoreBelowThreshold()
    {
        var h = new BossHarness(seed: 9008, potReward: new PotRewardConfig { ChoiceCount = 0 });

        WalkPot(h, true); SettlePot(h); h.Gc.AdvanceToNextPot();
        WalkPot(h, true); SettlePot(h); h.Gc.AdvanceToNextPot();
        WalkPot(h, satisfyBoss: false);

        var record = h.Gc.Run.ChapterBossRecords.Single();
        Assert(!record.Satisfied, "空锅嫌弃应记录为不满意");
        Assert(record.PotTotalFinalScore == 0, $"空锅本锅累计最终分应为 0，实际 {record.PotTotalFinalScore}");
        Assert(record.Threshold == h.Boss.GetChapterForm(1).SatisfyThreshold,
            "记录阈值应来自 BossConfig");
    }
}
