using SevenSpices.Core.Content;
using SevenSpices.Core.Customers;
using SevenSpices.Core.Effects;
using SevenSpices.Core.Game;
using SevenSpices.Core.Items;
using SevenSpices.Core.Pot;
using SevenSpices.Core.Run;
using SevenSpices.Core.Scoring;

namespace SevenSpices.Tests.Run;

/// <summary>
/// 饕餮 Boss 数据地基测试（方案 §八 B0 + B1）。
/// 只覆盖 B1 阶段可独立验证的数据层：MetaState、TotalFinalScore、新条件类型、
/// 饕餮定义、BossConfig、仙丹粉末与 SpecialRegistry、AddAllFlavorsEffect。
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

        Console.WriteLine("All BossSettlementTests passed.");
    }

    // ── B2 / B3 待补用例（本阶段刻意不写，避免未实现即红）─────────────────────
    //  - 第 3 / 6 / 9 锅第 10 碗被指派为对应章节形态的饕餮（B2）；
    //  - 章末满意 → 掉落仙丹粉末（Boss 专属流程，不走随机掉落）（B2）；
    //  - 章末嫌弃 → 本局终止，给保底评价「嫌弃，但是鼓励」（B2）；
    //  - 跨章各触发一次，不重复触发（B2）；
    //  - 最终锅走真身、结算后本局结束并记录结局（B3）；
    //  - Boss 判定路径无随机（B2）。

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
}
