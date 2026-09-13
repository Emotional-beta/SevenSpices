using SevenSpices.Core.Content;
using SevenSpices.Core.Game;
using SevenSpices.Core.Ingredients;
using SevenSpices.Core.Pot;

namespace SevenSpices.Tests.Game;

/// <summary>
/// 锅结束奖励（设计文档 §17）测试。
/// 覆盖：普通锅结束 X 选 1、候选互不重复且来自 Registry、必选 1 个进入食材篮、
/// 奖励门控「进入下一锅」、X 可调、非法选择、最终锅不触发、重复选择。
/// 固定种子 Random 保证可复现。
/// </summary>
public static class PotRewardTests
{
    public static void RunAll()
    {
        Test_NormalPotEnd_AwaitsReward_And_BlocksAdvance();
        Test_ChooseReward_AddsToBasket_And_UnlocksAdvance();
        Test_Reward_GrowsNextPotPool();
        Test_ChoiceCount_IsConfigurable();
        Test_AdvanceBeforeChoose_Throws();
        Test_InvalidChoice_Throws();
        Test_FinalPot_NoReward_RunComplete();
        Test_ChooseReward_Twice_Throws();

        Console.WriteLine("All PotRewardTests passed.");
    }

    // ── 辅助 ─────────────────────────────────────────────────────────────────

    /// <summary>关闭稀有食客，避免随机掉落干扰食材篮计数。</summary>
    static CustomerAppearanceConfig NoRare() =>
        new() { RareBowlNumbers = Array.Empty<int>() };

    /// <summary>关闭商店（陈列数量为 0），使奖励测试不受商店门控影响。</summary>
    static ShopConfig NoShop() =>
        new() { IngredientOfferCount = 0, ItemOfferCount = 0 };

    /// <summary>用「能选就选、池空就跳」走到当前普通锅 Ended，但<b>不</b>解决奖励。</summary>
    static void FinishPotWithoutReward(GameController gc)
    {
        int guard = 0;
        while (gc.Pot.Phase == PotPhase.InProgress && guard++ < 500)
        {
            if (gc.CanSelectIngredient)
                gc.SelectIngredient(gc.CurrentCandidates[0].InstanceId);
            else if (gc.CanSkipBowl)
                gc.SkipBowl();
            else
                break;
        }

        Assert(gc.Pot.Phase == PotPhase.Ended, "FinishPotWithoutReward: 普通锅应已 Ended");
    }

    /// <summary>走完普通锅并选定第一个奖励，便于连续推进多锅。</summary>
    static void FinishPotAndResolveReward(GameController gc)
    {
        FinishPotWithoutReward(gc);
        Assert(gc.IsAwaitingReward, "FinishPotAndResolveReward: 普通锅结束应处于奖励态");
        gc.ChooseReward(gc.RewardCandidates[0].InstanceId);
    }

    // ── 测试 ─────────────────────────────────────────────────────────────────

    /// <summary>1. 普通锅走完：进入奖励态、3 个互不重复且来自 Registry 的候选、未选定不可推进。</summary>
    static void Test_NormalPotEnd_AwaitsReward_And_BlocksAdvance()
    {
        var state = new GameState();
        var gc = new GameController(state, NoRare(), new Random(101), new PotRewardConfig(), NoShop());
        gc.StartNewGame();

        FinishPotWithoutReward(gc);

        Assert(gc.IsAwaitingReward, "普通锅结束后应处于奖励等待态");
        Assert(gc.CanChooseReward, "奖励待选时 CanChooseReward 应为 true");
        Assert(gc.RewardChoiceCount == 3, $"默认 X 应为 3，实际 {gc.RewardChoiceCount}");
        Assert(gc.RewardCandidates.Count == 3, $"默认应生成 3 个候选，实际 {gc.RewardCandidates.Count}");
        Assert(!gc.CanAdvanceToNextPot, "未选定奖励前不应可推进");

        var ids = gc.RewardCandidates.Select(r => r.Definition.Id).ToList();
        Assert(ids.Distinct().Count() == ids.Count, "奖励候选不得重复");
        var legal = IngredientData.Registry.GetAll().Select(d => d.Id).ToHashSet();
        Assert(ids.All(legal.Contains), "奖励候选应全部来自正式 Registry");
    }

    /// <summary>2. ChooseReward 后：篮 +1 且为选中实例、候选清空、可推进。</summary>
    static void Test_ChooseReward_AddsToBasket_And_UnlocksAdvance()
    {
        var state = new GameState();
        var gc = new GameController(state, NoRare(), new Random(102), new PotRewardConfig(), NoShop());
        gc.StartNewGame();

        FinishPotWithoutReward(gc);
        var chosen = gc.RewardCandidates[0];
        int basketBefore = gc.Player.IngredientBasket.Count;

        gc.ChooseReward(chosen.InstanceId);

        Assert(gc.Player.IngredientBasket.Count == basketBefore + 1,
            "选定奖励后食材篮应 +1");
        Assert(ReferenceEquals(gc.Player.IngredientBasket[^1], chosen),
            "进入食材篮的应是选中的那个实例");
        Assert(gc.RewardCandidates.Count == 0, "选定后奖励候选应清空");
        Assert(!gc.IsAwaitingReward, "选定后不应再处于奖励态");
        Assert(gc.CanAdvanceToNextPot, "选定后应可推进到下一锅");
    }

    /// <summary>
    /// 3. 奖励进入下一锅池：新锅池数量反映篮的增长（6 → 7），且身份校验——
    /// 走完第 2 锅并记录所有出现过的候选 InstanceId，选中的奖励实例必须确实来自本锅池。
    /// </summary>
    static void Test_Reward_GrowsNextPotPool()
    {
        var state = new GameState();
        var gc = new GameController(state, NoRare(), new Random(103), new PotRewardConfig(), NoShop());
        gc.StartNewGame();

        int basketBeforeReward = gc.Player.IngredientBasket.Count;
        Assert(basketBeforeReward == 6, $"A2 初始食材篮应为 6，实际 {basketBeforeReward}");

        FinishPotWithoutReward(gc);
        var chosen = gc.RewardCandidates[0];
        gc.ChooseReward(chosen.InstanceId);
        Assert(gc.Player.IngredientBasket.Count == basketBeforeReward + 1,
            "选定奖励后食材篮应增长为 7");
        Assert(gc.Player.IngredientBasket.Contains(chosen),
            "选中的奖励实例应进入食材篮");

        gc.AdvanceToNextPot();

        Assert(gc.Run.PotIndex == 2, "应进入第 2 锅");
        Assert(gc.RemainingPoolCount == basketBeforeReward + 1,
            $"新锅池应反映篮的增长（7），实际 {gc.RemainingPoolCount}");

        // 下一锅池是本锅开始时对食材篮的快照；走完整锅会把池内每个实例各移除一次，
        // 因而每个池内实例都必然在某碗被抽出。若奖励加错了实例，chosen 不在池中就不会出现。
        var basketIds = gc.Player.IngredientBasket.Select(i => i.InstanceId).ToHashSet();
        var seenInPool = gc.CurrentCandidates.Select(c => c.InstanceId).ToHashSet();

        int guard = 0;
        while (gc.Pot.Phase == PotPhase.InProgress && guard++ < 500)
        {
            if (gc.CanSelectIngredient)
            {
                gc.SelectIngredient(gc.CurrentCandidates[0].InstanceId);
                foreach (var c in gc.CurrentCandidates)
                    seenInPool.Add(c.InstanceId);
            }
            else if (gc.CanSkipBowl)
                gc.SkipBowl();
            else
                break;
        }

        Assert(seenInPool.All(basketIds.Contains),
            "第 2 锅抽出的候选应全部来自食材篮");
        Assert(seenInPool.Contains(chosen.InstanceId),
            "选中的奖励实例应确实出现在第 2 锅池（候选）中");
    }

    /// <summary>4. X 可调：注入 ChoiceCount=5 → 生成 5 个互不重复候选。</summary>
    static void Test_ChoiceCount_IsConfigurable()
    {
        var state = new GameState();
        var gc = new GameController(state, NoRare(), new Random(104),
            new PotRewardConfig { ChoiceCount = 5 }, NoShop());
        gc.StartNewGame();

        FinishPotWithoutReward(gc);

        Assert(gc.RewardChoiceCount == 5, $"RewardChoiceCount 应为注入的 5，实际 {gc.RewardChoiceCount}");
        Assert(gc.RewardCandidates.Count == 5,
            $"X=5 时应生成 5 个候选，实际 {gc.RewardCandidates.Count}");
        var ids = gc.RewardCandidates.Select(r => r.Definition.Id).ToList();
        Assert(ids.Distinct().Count() == 5, "5 个候选应互不重复");
    }

    /// <summary>5. 未选不能推进：奖励态下调 AdvanceToNextPot 抛异常且不推进。</summary>
    static void Test_AdvanceBeforeChoose_Throws()
    {
        var state = new GameState();
        var gc = new GameController(state, NoRare(), new Random(105), new PotRewardConfig(), NoShop());
        gc.StartNewGame();

        FinishPotWithoutReward(gc);
        Assert(gc.IsAwaitingReward, "应处于奖励态");

        bool threw = false;
        try { gc.AdvanceToNextPot(); }
        catch (InvalidOperationException) { threw = true; }

        Assert(threw, "未选奖励时 AdvanceToNextPot 应抛 InvalidOperationException");
        Assert(gc.Run.PotIndex == 1, "抛异常后 RunState 不应推进");
        Assert(gc.IsAwaitingReward, "抛异常后仍应处于奖励态");
    }

    /// <summary>6. 非法选择：非奖励态抛 InvalidOperationException；奖励态传不存在 id 抛 ArgumentException。</summary>
    static void Test_InvalidChoice_Throws()
    {
        var state = new GameState();
        var gc = new GameController(state, NoRare(), new Random(106), new PotRewardConfig(), NoShop());
        gc.StartNewGame();

        bool inProgressThrew = false;
        try { gc.ChooseReward("none"); }
        catch (InvalidOperationException) { inProgressThrew = true; }
        Assert(inProgressThrew, "非奖励态 ChooseReward 应抛 InvalidOperationException");

        FinishPotWithoutReward(gc);

        bool badIdThrew = false;
        try { gc.ChooseReward("not_a_reward_candidate"); }
        catch (ArgumentException) { badIdThrew = true; }
        Assert(badIdThrew, "奖励态传入不存在的 id 应抛 ArgumentException");
        Assert(gc.IsAwaitingReward, "非法选择不应消耗奖励态");
    }

    /// <summary>7. 最终锅无奖励：9 锅后 EndCooking → 无奖励态且 RunComplete。</summary>
    static void Test_FinalPot_NoReward_RunComplete()
    {
        var state = new GameState();
        var gc = new GameController(state, NoRare(), new Random(107), new PotRewardConfig(), NoShop());
        gc.StartNewGame();

        for (int i = 0; i < 9; i++)
        {
            FinishPotAndResolveReward(gc);
            gc.AdvanceToNextPot();
        }

        Assert(gc.IsFinalPot, "9 锅普通锅后应进入最终锅");
        Assert(!gc.IsAwaitingReward, "最终锅不应触发锅结束奖励");

        int guard = 0;
        while (gc.CanSelectIngredient && guard++ < 100)
            gc.SelectIngredient(gc.CurrentCandidates[0].InstanceId);

        gc.EndCooking();

        Assert(!gc.IsAwaitingReward, "EndCooking 后不应处于奖励态");
        Assert(gc.IsRunComplete, "EndCooking 后整局应完成");
    }

    /// <summary>8. 重复使用：第二次 ChooseReward 抛 InvalidOperationException。</summary>
    static void Test_ChooseReward_Twice_Throws()
    {
        var state = new GameState();
        var gc = new GameController(state, NoRare(), new Random(108), new PotRewardConfig(), NoShop());
        gc.StartNewGame();

        FinishPotWithoutReward(gc);
        gc.ChooseReward(gc.RewardCandidates[0].InstanceId);

        bool threw = false;
        try { gc.ChooseReward("none"); }
        catch (InvalidOperationException) { threw = true; }

        Assert(threw, "重复 ChooseReward 应抛 InvalidOperationException");
    }

    static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception($"[FAIL] PotRewardTests: {message}");
    }
}
