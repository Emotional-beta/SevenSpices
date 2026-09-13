using SevenSpices.Core.Content;
using SevenSpices.Core.Game;
using SevenSpices.Core.Ingredients;
using SevenSpices.Core.Pot;

namespace SevenSpices.Tests.Game;

/// <summary>
/// A4：从第 1 锅打通到最终锅完成的整局推进测试。
/// 覆盖普通锅结束提炼锅底、跨锅/跨章推进、非法推进、避免重复提炼、
/// 以及完整一局（9 锅普通锅 + 最终锅 EndCooking）的完成闭环。
/// </summary>
public static class RunProgressionTests
{
    public static void RunAll()
    {
        Test_NormalPotEnd_ExtractsBottom_And_CanAdvance();
        Test_AdvanceToNextPot_AdvancesRunState();
        Test_AdvanceToNextPot_WhileInProgress_Throws();
        Test_PotEnd_ExtractIsNotRepeated();
        Test_FullRun_ToFinalPot_EndCooking_Completes();
        Test_IsRunComplete_FalseBeforeFinalSettlement();

        Console.WriteLine("All RunProgressionTests passed.");
    }

    // ── 辅助 ─────────────────────────────────────────────────────────────────

    /// <summary>用「能选就选、池空就跳」走完当前普通锅（10 碗）。</summary>
    static void FinishCurrentPot(GameController gc)
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

        Assert(gc.Pot.Phase == PotPhase.Ended, "FinishCurrentPot: 普通锅应已 Ended");

        // 普通锅结束会有 X 选 1 奖励且门控「进入下一锅」，辅助方法代选第一个以便继续推进。
        if (gc.IsAwaitingReward)
            gc.ChooseReward(gc.RewardCandidates[0].InstanceId);
    }

    /// <summary>用固定数量的糖填满食材篮，使本锅甜味确定性累积。</summary>
    static void FillBasketWithSugar(GameState state, int count)
    {
        state.Player.IngredientBasket.Clear();
        for (int i = 0; i < count; i++)
            state.Player.IngredientBasket.Add(IngredientData.CreateInstance("sugar"));
    }

    // ── 测试 ─────────────────────────────────────────────────────────────────

    /// <summary>普通锅投满 10 个糖走到第 10 碗 Ended：锅底按 30% 向下取整提炼，且可推进。</summary>
    static void Test_NormalPotEnd_ExtractsBottom_And_CanAdvance()
    {
        var state = new GameState();
        FillBasketWithSugar(state, 10);

        var gc = new GameController(state, random: new Random(7));
        gc.StartNewGame();

        Assert(gc.Pot.Phase == PotPhase.InProgress, "开局锅应 InProgress");
        Assert(!gc.CanAdvanceToNextPot, "锅进行中不应可推进");

        FinishCurrentPot(gc);

        Assert(gc.Pot.GetFlavor(FlavorType.Sweet) == 10, "投入 10 个糖后本锅甜味应为 10");
        Assert(state.Bottom.GetFlavor(FlavorType.Sweet) == 3,
            $"10 × 30% = 3，锅底 Sweet 应为 3，实际 {state.Bottom.GetFlavor(FlavorType.Sweet)}");
        Assert(state.Bottom.GetFlavor(FlavorType.Umami) == 0,
            "本锅未出现过鲜味，不应写入锅底");
        Assert(gc.CanAdvanceToNextPot, "普通锅结束后 CanAdvanceToNextPot 应为 true");
        Assert(!gc.IsRunComplete, "普通锅结束不代表整局完成");
    }

    /// <summary>走完一锅 → AdvanceToNextPot：PotIndex++、跨章 Chapter++，新锅进行中且不可再推进。</summary>
    static void Test_AdvanceToNextPot_AdvancesRunState()
    {
        var state = new GameState();
        var gc = new GameController(state, random: new Random(11));
        gc.StartNewGame();

        FinishCurrentPot(gc);
        Assert(gc.CanAdvanceToNextPot, "第 1 锅结束后应可推进");
        gc.AdvanceToNextPot();

        Assert(gc.Run.Chapter == 1 && gc.Run.PotIndex == 2,
            $"第 1 锅后应进入 (1,2)，实际 ({gc.Run.Chapter},{gc.Run.PotIndex})");
        Assert(gc.Pot.Phase == PotPhase.InProgress, "第 2 锅应为 InProgress");
        Assert(!gc.CanAdvanceToNextPot, "新锅进行中不应可推进");

        FinishCurrentPot(gc);
        gc.AdvanceToNextPot();
        Assert(gc.Run.Chapter == 1 && gc.Run.PotIndex == 3, "第 2 锅后应进入 (1,3)");

        FinishCurrentPot(gc);
        gc.AdvanceToNextPot();
        Assert(gc.Run.Chapter == 2 && gc.Run.PotIndex == 1,
            $"完成 Chapter 1 后应跨章进入 (2,1)，实际 ({gc.Run.Chapter},{gc.Run.PotIndex})");
        Assert(!gc.CanAdvanceToNextPot, "跨章后新锅进行中不应可推进");
    }

    /// <summary>锅仍在 InProgress 时调用 AdvanceToNextPot 应抛异常且不推进 RunState。</summary>
    static void Test_AdvanceToNextPot_WhileInProgress_Throws()
    {
        var gc = new GameController();
        gc.StartNewGame();

        Assert(gc.Pot.Phase == PotPhase.InProgress, "初始锅应 InProgress");

        bool threw = false;
        try { gc.AdvanceToNextPot(); }
        catch (InvalidOperationException) { threw = true; }

        Assert(threw, "锅未结束时 AdvanceToNextPot 应抛 InvalidOperationException");
        Assert(gc.Run.Chapter == 1 && gc.Run.PotIndex == 1, "抛异常后 RunState 不应推进");
    }

    /// <summary>
    /// 行为性证明「锅结束只提炼一次」：锅 Ended 后经由公开 API 不存在任何再次进入
    /// 结束分支的路径——SelectIngredient / SkipBowl 均被公开守卫拒绝并抛异常，
    /// 锅阶段与锅底保持不变，因此不可能发生重复提炼。
    /// （证明边界：覆盖所有公开的锅级收尾入口；不伪造内部重入。）
    /// </summary>
    static void Test_PotEnd_ExtractIsNotRepeated()
    {
        var state = new GameState();
        FillBasketWithSugar(state, 10);

        var gc = new GameController(state, random: new Random(13));
        gc.StartNewGame();
        FinishCurrentPot(gc);

        int sweetAfterFirst = state.Bottom.GetFlavor(FlavorType.Sweet);
        Assert(sweetAfterFirst == 3, $"首次提炼 Sweet 应为 3，实际 {sweetAfterFirst}");

        // 锅已 Ended，公开守卫应全部为 false。
        Assert(!gc.CanSelectIngredient, "锅 Ended 后 CanSelectIngredient 应为 false");
        Assert(!gc.CanSkipBowl, "锅 Ended 后 CanSkipBowl 应为 false");

        bool selectThrew = false;
        try { gc.SelectIngredient("sugar"); }
        catch (InvalidOperationException) { selectThrew = true; }

        bool skipThrew = false;
        try { gc.SkipBowl(); }
        catch (InvalidOperationException) { skipThrew = true; }

        Assert(selectThrew, "锅 Ended 后 SelectIngredient 应抛 InvalidOperationException");
        Assert(skipThrew, "锅 Ended 后 SkipBowl 应抛 InvalidOperationException");
        Assert(gc.Pot.Phase == PotPhase.Ended, "被拒绝的调用不应改变锅阶段");
        Assert(state.Bottom.GetFlavor(FlavorType.Sweet) == sweetAfterFirst,
            "被拒绝的调用不应放大锅底");
        Assert(gc.CanAdvanceToNextPot, "锅结束后仍应保持可推进");
    }

    /// <summary>
    /// 完整一局：9 锅普通锅 → 最终锅 → 投入食材 → EndCooking → IsRunComplete。
    /// 显式关闭稀有食客（RareBowlNumbers 为空），使本测试只验证推进/完成，
    /// 不受稀有掉落引入的额外食材干扰。
    /// </summary>
    static void Test_FullRun_ToFinalPot_EndCooking_Completes()
    {
        var state = new GameState();
        var appearance = new CustomerAppearanceConfig { RareBowlNumbers = Array.Empty<int>() };
        // 关闭锅结束奖励（ChoiceCount = 0），本测试只验证推进/完成与锅底提炼，
        // 避免随机奖励食材通过锅底影响「最终锅结算前无酸味」的既有前提。
        var potReward = new PotRewardConfig { ChoiceCount = 0 };
        var gc = new GameController(state, appearance, new Random(2026), potReward);
        gc.StartNewGame();

        for (int i = 0; i < 9; i++)
        {
            FinishCurrentPot(gc);
            Assert(gc.CanAdvanceToNextPot, $"第 {i + 1} 锅结束后应可推进");

            if (i == 8)
            {
                // 进入最终锅前替换篮为醋，用于验证最终锅结算后锅底确实被提炼。
                state.Player.IngredientBasket.Clear();
                for (int j = 0; j < 5; j++)
                    state.Player.IngredientBasket.Add(IngredientData.CreateInstance("vinegar"));
            }

            gc.AdvanceToNextPot();
        }

        Assert(gc.IsFinalPot && gc.Run.IsFinalPot, "9 锅普通锅后应进入最终锅");
        Assert(!gc.IsRunComplete, "最终锅结算前 IsRunComplete 应为 false");
        Assert(!gc.CanAdvanceToNextPot, "最终锅不应可推进到下一锅");

        // 本测试已关闭锅结束奖励（ChoiceCount = 0），普通锅结束时不会把随机食材带入篮子，
        // 因此结算前锅底不含奖励带来的酸味；记录基准后验证最终锅结算恰好再提炼 +floor(5×0.3)=1。
        int sourBeforeFinalSettlement = state.Bottom.GetFlavor(FlavorType.Sour);

        int selected = 0;
        while (gc.CanSelectIngredient && selected < 20)
        {
            gc.SelectIngredient(gc.CurrentCandidates[0].InstanceId);
            selected++;
        }

        Assert(selected == 5, $"最终锅应投入全部 5 个醋，实际 {selected}");
        Assert(state.Pot.GetFlavor(FlavorType.Sour) == 5, "最终锅酸味应累积为 5");

        gc.EndCooking();

        Assert(gc.Pot.Phase == PotPhase.Ended, "EndCooking 后最终锅应 Ended");
        Assert(gc.IsRunComplete, "EndCooking 后 IsRunComplete 应为 true");
        Assert(!gc.CanAdvanceToNextPot, "完成后 CanAdvanceToNextPot 应为 false");
        Assert(state.Bottom.GetFlavor(FlavorType.Sour) == sourBeforeFinalSettlement + 1,
            $"最终锅结算后锅底 Sour 应比结算前多 floor(5×0.3)=1，实际 {state.Bottom.GetFlavor(FlavorType.Sour)}");
    }

    /// <summary>IsRunComplete 在最终锅结算前始终为 false（普通锅进行中/结束后、最终锅进行中）。</summary>
    static void Test_IsRunComplete_FalseBeforeFinalSettlement()
    {
        var state = new GameState();
        var gc = new GameController(state, random: new Random(31));
        gc.StartNewGame();

        Assert(!gc.IsRunComplete, "普通锅进行中 IsRunComplete 应为 false");
        FinishCurrentPot(gc);
        Assert(!gc.IsRunComplete, "普通锅结束后 IsRunComplete 仍应为 false");

        for (int pot = 2; pot <= 9; pot++)
        {
            gc.AdvanceToNextPot();
            FinishCurrentPot(gc);
            Assert(!gc.IsRunComplete, $"第 {pot} 锅结束后 IsRunComplete 仍应为 false");
        }

        gc.AdvanceToNextPot();
        Assert(gc.IsFinalPot, "应进入最终锅");
        Assert(!gc.IsRunComplete, "最终锅进行中 IsRunComplete 应为 false");
    }

    static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception($"[FAIL] RunProgressionTests: {message}");
    }
}
