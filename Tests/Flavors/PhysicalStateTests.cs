using SevenSpices.Core.Bottom;
using SevenSpices.Core.Content;
using SevenSpices.Core.Flavors;
using SevenSpices.Core.Game;
using SevenSpices.Core.Ingredients;
using SevenSpices.Core.Pot;
using SevenSpices.Core.Scoring;

namespace SevenSpices.Tests.Flavors;

/// <summary>
/// 味道系统 F5 测试：通用物理状态容器 + 「臭」。
/// 覆盖：触发条件与幂等、丰盛倍率失效、锅末「现实转移」（最弱非鲜 / 并列取最早 / 剔除鲜自身 /
/// 锅底全 0 不动作）、咸·固化免疫、快照与 Reset、锅末「提炼 → 现实转移」顺序。
/// </summary>
public static class PhysicalStateTests
{
    public static void RunAll()
    {
        Test_StatusContainer_Generic_AddSetHasClear();
        Test_Odor_Trigger_And_Idempotent();
        Test_Odor_NotTriggered_BelowThreshold();
        Test_Odor_Disables_Abundance();
        Test_Odor_RealityTransfer_WeakestNonUmami();
        Test_Odor_RealityTransfer_TieBreak_And_ExcludeUmami();
        Test_Odor_RealityTransfer_EmptyBottom_NoAction();
        Test_Odor_Solidified_Immune();
        Test_StatusContainer_Snapshot_And_Reset();
        Test_ClosePot_OdorTransfer_AfterExtract();

        Console.WriteLine("All PhysicalStateTests passed.");
    }

    // ── 辅助 ─────────────────────────────────────────────────────────────────

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

    // ── 1. 通用容器 ───────────────────────────────────────────────────────────

    static void Test_StatusContainer_Generic_AddSetHasClear()
    {
        var container = new PotStatusContainer();
        Assert(container.Count == 0, "新容器应为空");
        Assert(!container.Has("x"), "未激活状态 Has 应为 false");
        Assert(container.Get("x") == 0, "未激活状态强度应为 0");

        container.Add("x");
        Assert(container.Has("x") && container.Get("x") == 1, "Add 应以强度 1 激活");

        container.Add("x", 5);
        Assert(container.Get("x") == 5, "Add 可提升强度");
        container.Add("x", 2);
        Assert(container.Get("x") == 5, "Add 幂等：只增不减，低强度不覆盖");

        container.Set("x", 3);
        Assert(container.Get("x") == 3, "Set 应覆盖强度");
        container.Set("x", 0);
        Assert(!container.Has("x"), "Set ≤ 0 应移除状态");

        container.Add("y");
        container.Add("z");
        Assert(container.Count == 2, "应记录 2 个激活状态");
        container.Clear();
        Assert(container.Count == 0, "Clear 应清空全部状态");
    }

    // ── 2. 触发条件 + 幂等 ────────────────────────────────────────────────────

    static void Test_Odor_Trigger_And_Idempotent()
    {
        var config = new FlavorConfig();

        var pot = new PotState { Config = config };
        pot.AddFlavor(FlavorType.Umami, 3);
        pot.AddFlavor(FlavorType.Bitter, 3);
        FlavorInteractionSystem.Default.Resolve(pot, MakeIngredient(0));
        Assert(pot.HasOdor, "鲜≥3 且 苦≥3 应触发臭");
        Assert(pot.Statuses.Count == 1, "应只激活 1 个物理状态");
        Assert(pot.Statuses.Get(PotStatusIds.Odor) == 1, "臭强度应为 1");

        // 重复加料幂等：仍只激活一次、强度不累加。
        FlavorInteractionSystem.Default.Resolve(pot, MakeIngredient(0));
        FlavorInteractionSystem.Default.Resolve(pot, MakeIngredient(0));
        Assert(pot.Statuses.Count == 1 && pot.Statuses.Get(PotStatusIds.Odor) == 1,
            "重复判定应幂等（每锅最多一次、强度不累加）");
    }

    static void Test_Odor_NotTriggered_BelowThreshold()
    {
        var config = new FlavorConfig();

        var lowBitter = new PotState { Config = config };
        lowBitter.AddFlavor(FlavorType.Umami, 3);
        lowBitter.AddFlavor(FlavorType.Bitter, 2);
        FlavorInteractionSystem.Default.Resolve(lowBitter, MakeIngredient(0));
        Assert(!lowBitter.HasOdor, "苦未达阈值不应触发臭");

        var lowUmami = new PotState { Config = config };
        lowUmami.AddFlavor(FlavorType.Umami, 2);
        lowUmami.AddFlavor(FlavorType.Bitter, 3);
        FlavorInteractionSystem.Default.Resolve(lowUmami, MakeIngredient(0));
        Assert(!lowUmami.HasOdor, "鲜未达阈值不应触发臭");
    }

    // ── 3. 代价：丰盛倍率失效 ─────────────────────────────────────────────────

    static void Test_Odor_Disables_Abundance()
    {
        // 对照：3 种味道 → 1 + 0.1×2 = 1.2。
        var normal = new PotState { BowlNumber = 1, BaseScore = 8 };
        normal.AddFlavor(FlavorType.Sweet, 1);
        normal.AddFlavor(FlavorType.Salty, 1);
        normal.AddFlavor(FlavorType.Sour, 1);
        AssertClose(ScoreCalculator.GetAbundanceMultiplier(normal), 1.2, "无臭时 3 种味道丰盛应为 1.2");

        // 臭激活：即便多种味道，丰盛倍率也应为 1。
        var odor = new PotState { BowlNumber = 1, BaseScore = 8 };
        odor.AddFlavor(FlavorType.Sweet, 1);
        odor.AddFlavor(FlavorType.Salty, 1);
        odor.AddFlavor(FlavorType.Sour, 1);
        odor.Statuses.Add(PotStatusIds.Odor);
        AssertClose(ScoreCalculator.GetAbundanceMultiplier(odor), 1.0, "臭激活时丰盛倍率应失效为 1");

        // 纳入最终分：味道分 3、基础分 8 → 11；无臭 floor(11×1.2)=13，有臭 floor(11×1)=11。
        Assert(ScoreCalculator.ComputeFinalScore(normal) == 13, "无臭时最终分应含丰盛：floor(11×1.2)=13");
        Assert(ScoreCalculator.ComputeFinalScore(odor) == 11, "有臭时最终分应不含丰盛：floor(11×1)=11");
    }

    // ── 4. 现实转移：最弱非鲜 ─────────────────────────────────────────────────

    static void Test_Odor_RealityTransfer_WeakestNonUmami()
    {
        var pot = new PotState();
        pot.Statuses.Add(PotStatusIds.Odor);

        // 锅底 {甜:5, 辣:3, 鲜:1}：剔除鲜后最弱为辣(3) → 辣归 0，鲜 1+3=4。
        var bottom = new BottomState();
        bottom.SetFlavor(FlavorType.Sweet, 5);
        bottom.SetFlavor(FlavorType.Spicy, 3);
        bottom.SetFlavor(FlavorType.Umami, 1);

        FlavorStatusRules.ApplyOdorRealityTransfer(pot, bottom);

        Assert(bottom.GetFlavor(FlavorType.Sweet) == 5, "甜味应保持不变");
        Assert(bottom.GetFlavor(FlavorType.Spicy) == 0, "最弱非鲜的辣应全部转移，归 0");
        Assert(bottom.GetFlavor(FlavorType.Umami) == 4, "鲜应增加转移量：1 + 3 = 4");
        Assert(pot.GetFlavor(FlavorType.Umami) == 0, "现实转移不得改写 PotState");
    }

    static void Test_Odor_RealityTransfer_TieBreak_And_ExcludeUmami()
    {
        // 并列最弱：酸(0) 声明早于 甜(1)，取酸。
        var pot = new PotState();
        pot.Statuses.Add(PotStatusIds.Odor);

        var tie = new BottomState();
        tie.SetFlavor(FlavorType.Sour, 3);
        tie.SetFlavor(FlavorType.Sweet, 3);
        tie.SetFlavor(FlavorType.Umami, 1);
        FlavorStatusRules.ApplyOdorRealityTransfer(pot, tie);

        Assert(tie.GetFlavor(FlavorType.Sour) == 0, "并列最弱应按枚举声明顺序取酸");
        Assert(tie.GetFlavor(FlavorType.Sweet) == 3, "并列的甜不应被转移");
        Assert(tie.GetFlavor(FlavorType.Umami) == 4, "鲜应增加转移量：1 + 3 = 4");

        // 剔除鲜自身：锅底仅鲜 → 不动作（不出现「鲜转给自己」）。
        var onlyUmami = new BottomState();
        onlyUmami.SetFlavor(FlavorType.Umami, 5);
        FlavorStatusRules.ApplyOdorRealityTransfer(pot, onlyUmami);
        Assert(onlyUmami.GetFlavor(FlavorType.Umami) == 5, "仅剩鲜时现实转移应不动作");

        // 鲜与非鲜并存且鲜更弱：仍取最弱非鲜（甜 2）转移。
        var mixed = new BottomState();
        mixed.SetFlavor(FlavorType.Umami, 1);
        mixed.SetFlavor(FlavorType.Sweet, 2);
        FlavorStatusRules.ApplyOdorRealityTransfer(pot, mixed);
        Assert(mixed.GetFlavor(FlavorType.Sweet) == 0, "最弱非鲜（甜 2）应被转移");
        Assert(mixed.GetFlavor(FlavorType.Umami) == 3, "鲜应增加转移量：1 + 2 = 3");
    }

    static void Test_Odor_RealityTransfer_EmptyBottom_NoAction()
    {
        var pot = new PotState();
        pot.Statuses.Add(PotStatusIds.Odor);

        var bottom = new BottomState();
        // 不抛异常、不改动任何味道。
        FlavorStatusRules.ApplyOdorRealityTransfer(pot, bottom);
        foreach (FlavorType flavor in Enum.GetValues<FlavorType>())
            Assert(bottom.GetFlavor(flavor) == 0, "锅底全 0 时现实转移应不动作、不报错");

        // 臭未激活：也不动作。
        var cleanPot = new PotState();
        var bottom2 = new BottomState();
        bottom2.SetFlavor(FlavorType.Sweet, 5);
        FlavorStatusRules.ApplyOdorRealityTransfer(cleanPot, bottom2);
        Assert(bottom2.GetFlavor(FlavorType.Sweet) == 5, "臭未激活时不得改写锅底");
    }

    // ── 5. 咸·固化免疫 ────────────────────────────────────────────────────────

    static void Test_Odor_Solidified_Immune()
    {
        var config = new FlavorConfig();

        // 固化时臭不触发。
        var pot = new PotState { Config = config };
        pot.IsSolidified = true;
        pot.AddFlavor(FlavorType.Umami, 5);
        pot.AddFlavor(FlavorType.Bitter, 5);
        FlavorInteractionSystem.Default.Resolve(pot, MakeIngredient(0));
        Assert(!pot.HasOdor, "咸·固化时臭不应触发");

        // 即便臭已激活，固化后也不做现实转移。
        var solidified = new PotState();
        solidified.Statuses.Add(PotStatusIds.Odor);
        solidified.IsSolidified = true;
        var bottom = new BottomState();
        bottom.SetFlavor(FlavorType.Sweet, 5);
        bottom.SetFlavor(FlavorType.Umami, 1);
        FlavorStatusRules.ApplyOdorRealityTransfer(solidified, bottom);
        Assert(bottom.GetFlavor(FlavorType.Sweet) == 5, "固化应免疫现实转移（甜不变）");
        Assert(bottom.GetFlavor(FlavorType.Umami) == 1, "固化应免疫现实转移（鲜不变）");
    }

    // ── 6. 快照 / Reset ───────────────────────────────────────────────────────

    static void Test_StatusContainer_Snapshot_And_Reset()
    {
        var pot = new PotState();
        pot.Statuses.Add(PotStatusIds.Odor, 2);

        var snap = PotStateSnapshot.From(pot);
        Assert(snap.HasOdor, "快照应深拷贝物理状态");
        Assert(snap.Statuses.Get(PotStatusIds.Odor) == 2, "快照应保留状态强度");

        // 清空快照不应影响真实状态。
        snap.Statuses.Clear();
        Assert(pot.HasOdor && pot.Statuses.Get(PotStatusIds.Odor) == 2,
            "清空快照不应影响真实锅的物理状态");

        // Reset 清空物理状态。
        pot.Reset();
        Assert(!pot.HasOdor, "Reset 后臭应被清空");
        Assert(pot.Statuses.Count == 0, "Reset 应清空全部物理状态");
    }

    // ── 7. 锅末顺序：提炼 → 现实转移 ──────────────────────────────────────────

    static void Test_ClosePot_OdorTransfer_AfterExtract()
    {
        var state = new GameState();
        var ctrl = new PotController(state);
        ctrl.StartPot();

        state.Pot.AddFlavor(FlavorType.Umami, 10);
        state.Pot.AddFlavor(FlavorType.Bitter, 10);

        // 通过一次加料的物理状态检查激活臭。
        FlavorInteractionSystem.Default.Resolve(state.Pot, MakeIngredient(0));
        Assert(state.Pot.HasOdor, "构造前提：臭应已激活");

        ctrl.EndPot();
        ctrl.ClosePot();

        // Extract：鲜 10→3、苦 10→3。若现实转移在提炼之后生效，最弱非鲜（苦 3）应转给鲜 → 苦 0、鲜 6。
        // 若顺序颠倒（先转移后提炼），锅底此刻全 0，转移无动作，最终会得到 鲜 3、苦 3。
        Assert(state.Bottom.GetFlavor(FlavorType.Umami) == 6,
            $"现实转移应在提炼之后：鲜应为 3+3=6，实际 {state.Bottom.GetFlavor(FlavorType.Umami)}");
        Assert(state.Bottom.GetFlavor(FlavorType.Bitter) == 0,
            $"最弱非鲜（苦）应归 0，实际 {state.Bottom.GetFlavor(FlavorType.Bitter)}");
    }

    // ─────────────────────────────────────────────────────────────────────────

    static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception($"[FAIL] PhysicalStateTests: {message}");
    }

    static void AssertClose(double actual, double expected, string message)
    {
        if (Math.Abs(actual - expected) > 1e-9)
            throw new Exception($"[FAIL] PhysicalStateTests: {message}（期望 {expected}，实际 {actual}）");
    }
}
