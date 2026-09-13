using SevenSpices.Core.Content;
using SevenSpices.Core.Customers;
using SevenSpices.Core.Game;
using SevenSpices.Core.Ingredients;
using SevenSpices.Core.Pot;

namespace SevenSpices.Tests.Game;

/// <summary>
/// A3：普通锅碗级食客流程闭环测试。
/// 覆盖每碗指派、普通食客金币（可调）、稀有出现可控、稀有满意掉落与不满意无奖励、
/// 以及食客状态按「本锅」跨锅重置。
/// 固定种子 Random 同时注入食客出现与食材池抽取，因此候选与食客均可复现。
/// </summary>
public static class CustomerFlowTests
{
    public static void RunAll()
    {
        Test_DefaultConfig_AssignsCustomerEveryBowl();
        Test_NormalCustomer_GoldEqualsTenAfterTenBowls();
        Test_BaseGoldReward_IsConfigurable();
        Test_RareBowlNumber_And_Probability_AreConfigurable();
        Test_RareCustomer_Satisfied_DropsRandomIngredient();
        Test_RareCustomer_NotSatisfied_NoDrop();
        Test_CustomerState_ResetsForNextPot();

        Console.WriteLine("All CustomerFlowTests passed.");
    }

    // ── 辅助 ─────────────────────────────────────────────────────────────────

    static CustomerAppearanceConfig AllNormalConfig(int baseGoldReward = 1) =>
        new()
        {
            BaseGoldReward = baseGoldReward,
            RareBowlNumbers = Array.Empty<int>(),
        };

    /// <summary>用「能选就选、池空就跳」走完当前普通锅（10 碗）。</summary>
    static void FinishNormalPot(GameController gc)
    {
        int guard = 0;
        while (gc.Pot.Phase == PotPhase.InProgress && guard++ < 200)
        {
            if (gc.CanSelectIngredient)
                gc.SelectIngredient(gc.CurrentCandidates[0].InstanceId);
            else if (gc.CanSkipBowl)
                gc.SkipBowl();
            else
                break;
        }

        Assert(gc.Pot.Phase == PotPhase.Ended, "FinishNormalPot: 普通锅应已 Ended");

        // 普通锅结束会有 X 选 1 奖励且门控「进入下一锅」，辅助方法代选第一个以便继续推进。
        if (gc.IsAwaitingReward)
            gc.ChooseReward(gc.RewardCandidates[0].InstanceId);
    }

    /// <summary>推进恰好一碗（能选就选、池空就跳），供逐碗断言使用。</summary>
    static void AdvanceOneBowl(GameController gc)
    {
        if (gc.CanSelectIngredient)
            gc.SelectIngredient(gc.CurrentCandidates[0].InstanceId);
        else if (gc.CanSkipBowl)
            gc.SkipBowl();
        else
            throw new Exception("[FAIL] CustomerFlowTests: 无法推进当前碗");
    }

    static CustomerDefinition MakeRareDefinition(int scoreThreshold) =>
        new(
            id: "test_rare",
            name: "测试稀有食客",
            isRare: true,
            satisfactionConditions: new[]
            {
                new CustomerSatisfactionCondition(ConditionType.ScoreAtLeast, scoreThreshold)
            });

    // ── 测试 ─────────────────────────────────────────────────────────────────

    /// <summary>默认配置下逐碗都指派食客，AppearedCustomers 随碗号逐一递增到 10。</summary>
    static void Test_DefaultConfig_AssignsCustomerEveryBowl()
    {
        var gc = new GameController(random: new Random(20260912));
        gc.StartNewGame();

        for (int bowl = 1; bowl <= 10; bowl++)
        {
            Assert(gc.CurrentCustomer != null, $"第 {bowl} 碗开始后应已指派食客");
            Assert(gc.State.Customer.AppearedCustomers.Count == bowl,
                $"第 {bowl} 碗后 AppearedCustomers 应为 {bowl}");

            AdvanceOneBowl(gc);
        }

        Assert(gc.Pot.Phase == PotPhase.Ended,
            "走完 10 碗后普通锅应已 Ended");
        Assert(gc.State.Customer.AppearedCustomers.Count == 10,
            "走完 10 碗后 AppearedCustomers 应为 10");
        Assert(gc.CurrentCustomer == null,
            "锅结束后最后一碗食客已结算，CurrentCustomer 应为 null");
    }

    /// <summary>全部普通食客、基础金币 1：10 碗后金币 == 10。</summary>
    static void Test_NormalCustomer_GoldEqualsTenAfterTenBowls()
    {
        var gc = new GameController(appearance: AllNormalConfig(), random: new Random(1));
        gc.StartNewGame();

        FinishNormalPot(gc);

        Assert(gc.Player.Gold == 10, "基础金币 1 × 10 碗 = 10");
    }

    /// <summary>基础金币可调：BaseGoldReward=3 → 10 碗后金币 == 30。</summary>
    static void Test_BaseGoldReward_IsConfigurable()
    {
        var gc = new GameController(appearance: AllNormalConfig(baseGoldReward: 3), random: new Random(2));
        gc.StartNewGame();

        FinishNormalPot(gc);

        Assert(gc.Player.Gold == 30, "基础金币 3 × 10 碗 = 30");
    }

    /// <summary>稀有出现可控：命中碗数 + 概率 1 → 稀有；概率 0 → 普通。</summary>
    static void Test_RareBowlNumber_And_Probability_AreConfigurable()
    {
        var rareConfig = new CustomerAppearanceConfig
        {
            RareBowlNumbers = new[] { 1 },
            RareProbability = 1.0,
        };
        var gcRare = new GameController(appearance: rareConfig, random: new Random(3));
        gcRare.StartNewGame();
        Assert(gcRare.CurrentCustomer != null && gcRare.CurrentCustomer.Definition.IsRare,
            "RareProbability=1.0 时第 1 碗应为稀有食客");

        var normalConfig = new CustomerAppearanceConfig
        {
            RareBowlNumbers = new[] { 1 },
            RareProbability = 0.0,
        };
        var gcNormal = new GameController(appearance: normalConfig, random: new Random(3));
        gcNormal.StartNewGame();
        Assert(gcNormal.CurrentCustomer != null && !gcNormal.CurrentCustomer.Definition.IsRare,
            "RareProbability=0.0 时第 1 碗应为普通食客");
    }

    /// <summary>稀有食客满意：掉落 1 个合法随机食材，并记录进 SatisfiedRareCustomers。</summary>
    static void Test_RareCustomer_Satisfied_DropsRandomIngredient()
    {
        var config = new CustomerAppearanceConfig
        {
            RareBowlNumbers = new[] { 1 },
            RareProbability = 1.0,
            RareCustomers = new[] { MakeRareDefinition(scoreThreshold: 1) }, // 极易满足
        };
        var gc = new GameController(appearance: config, random: new Random(4));
        gc.StartNewGame();

        var rareInstance = gc.CurrentCustomer;
        Assert(rareInstance != null && rareInstance.Definition.IsRare,
            "第 1 碗应指派稀有食客");
        Assert(rareInstance!.Definition.Name == "测试稀有食客",
            "应使用注入的稀有 Definition");

        int basketBefore = gc.Player.IngredientBasket.Count;
        Assert(basketBefore == 6, "A2 初始食材篮应为 6");

        // 投入 1 个食材（基础分 ≥ 1），锁定后 FinalScore ≥ 1 → 满足 ScoreAtLeast 1
        gc.SelectIngredient(gc.CurrentCandidates[0].InstanceId);

        Assert(gc.State.Customer.SatisfiedRareCustomers.Count == 1,
            "满意的稀有食客应进入 SatisfiedRareCustomers");
        Assert(ReferenceEquals(gc.State.Customer.SatisfiedRareCustomers[0], rareInstance),
            "记录的应为同一个稀有食客实例");
        Assert(gc.Player.IngredientBasket.Count == basketBefore + 1,
            "满意后食材篮应只增加掉落的那 1 个");

        var dropped = gc.Player.IngredientBasket[^1];
        var legalIds = IngredientData.Registry.GetAll().Select(d => d.Id).ToHashSet();
        Assert(legalIds.Contains(dropped.Definition.Id),
            "掉落食材应来自正式 Registry");
    }

    /// <summary>稀有食客不满意：无掉落、SatisfiedRareCustomers 为空。</summary>
    static void Test_RareCustomer_NotSatisfied_NoDrop()
    {
        var config = new CustomerAppearanceConfig
        {
            RareBowlNumbers = new[] { 1 },
            RareProbability = 1.0,
            RareCustomers = new[] { MakeRareDefinition(scoreThreshold: 9999) }, // 不可能满足
        };
        var gc = new GameController(appearance: config, random: new Random(5));
        gc.StartNewGame();

        int basketBefore = gc.Player.IngredientBasket.Count;

        gc.SelectIngredient(gc.CurrentCandidates[0].InstanceId);

        Assert(gc.State.Customer.SatisfiedRareCustomers.Count == 0,
            "不满意的稀有食客不应进入 SatisfiedRareCustomers");
        Assert(gc.Player.IngredientBasket.Count == basketBefore,
            "不满意时不应掉落食材");
    }

    /// <summary>
    /// 食客状态按「本锅」重置：第 1 锅结束后数据仍可读，进入第 2 锅（StartCurrentPot）
    /// 时 AppearedCustomers / SatisfiedRareCustomers 被清空，只含第 2 锅第 1 碗的食客。
    /// </summary>
    static void Test_CustomerState_ResetsForNextPot()
    {
        var config = new CustomerAppearanceConfig
        {
            RareBowlNumbers = new[] { 1 },
            RareProbability = 1.0,
            RareCustomers = new[] { MakeRareDefinition(scoreThreshold: 1) }, // 第 1 碗极易满足
        };
        var gc = new GameController(appearance: config, random: new Random(6));
        gc.StartNewGame();

        Assert(gc.State.Customer.SatisfiedRareCustomers.Count == 0,
            "第 1 锅开始时 SatisfiedRareCustomers 应为空");

        // 第 1 碗投入 1 个食材，满足稀有食客（阈值 1），记录进 SatisfiedRareCustomers。
        gc.SelectIngredient(gc.CurrentCandidates[0].InstanceId);
        Assert(gc.State.Customer.SatisfiedRareCustomers.Count == 1,
            "第 1 锅第 1 碗满意的稀有食客应被记录");

        FinishNormalPot(gc);
        Assert(gc.Pot.Phase == PotPhase.Ended, "第 1 锅应已 Ended");

        // 锅结束后、进入下一锅前：本锅数据仍可读（清空发生在下一锅 StartCurrentPot）。
        Assert(gc.State.Customer.AppearedCustomers.Count == 10,
            "第 1 锅结束后 AppearedCustomers 应仍为 10 可读");
        Assert(gc.State.Customer.SatisfiedRareCustomers.Count == 1,
            "第 1 锅结束后 SatisfiedRareCustomers 应仍为 1 可读");

        gc.AdvanceToNextPot();

        // 第 2 锅第 1 碗：状态已重置，只含本锅第 1 碗的食客。
        Assert(gc.CurrentCustomer != null, "第 2 锅第 1 碗应已指派食客");
        Assert(gc.State.Customer.AppearedCustomers.Count == 1,
            "第 2 锅开始时 AppearedCustomers 应重置为仅本锅第 1 碗 == 1");
        Assert(gc.State.Customer.SatisfiedRareCustomers.Count == 0,
            "第 2 锅开始时 SatisfiedRareCustomers 应已清空");
    }

    static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception($"[FAIL] CustomerFlowTests: {message}");
    }
}
