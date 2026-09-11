using SevenSpices.Core.Content;
using SevenSpices.Core.Effects;
using SevenSpices.Core.Game;
using SevenSpices.Core.Ingredients;
using SevenSpices.Core.Pot;

namespace SevenSpices.Tests.Content;

/// <summary>
/// 正式食材数据测试：验证 10 种食材的 Definition 正确性及效果行为。
/// Phase 4 Part 4。
/// </summary>
public static class IngredientDataTests
{
    public static void RunAll()
    {
        // Registry 完整性
        Test_Registry_HasTenIngredients();
        Test_Registry_AllIdsUnique();
        Test_Registry_CanGetByAllIds();

        // 基础属性
        Test_Rice_Attributes();
        Test_Sugar_Attributes();
        Test_Pepper_Attributes();
        Test_RedDate_Attributes();
        Test_Ginger_Attributes();
        Test_Vinegar_Attributes();
        Test_Honey_Attributes();
        Test_ChiliOil_Attributes();
        Test_IceCube_Attributes();
        Test_Egg_Attributes();

        // Instance 创建
        Test_CreateInstance_ReferencesCorrectDefinition();

        // 效果行为验证
        Test_RedDate_Effect_SweetGte3_AddsBonus();
        Test_RedDate_Effect_SweetLt3_NoBonus();
        Test_Ginger_Effect_SpicyGte3_AddsBonus();
        Test_Vinegar_Effect_SweetGte3_AddsBonus();
        Test_Vinegar_Effect_Triggers_OnSweet_NotSour();
        Test_Honey_Effect_Scaled_Sweet6();
        Test_Honey_Effect_Scaled_Sweet9();
        Test_Honey_Effect_Sweet0_NoBonus();
        Test_ChiliOil_Effect_Scaled_Spicy6();
        Test_IceCube_Effect_SetsFinalScoreMultiplier();
        Test_IceCube_Effect_AppliedInCalculateAndLock();
        Test_Egg_Effect_ThreeUniqueIngredients_AddsBonus();
        Test_Egg_Effect_TwoUniqueIngredients_NoBonus();
        Test_Egg_Effect_CountsByDefinitionNotInstance();

        Console.WriteLine("All IngredientDataTests passed.");
    }

    // ── 辅助 ─────────────────────────────────────────────────────────────────

    /// <summary>创建一个处于 IngredientResolve 阶段的 PotController，并返回状态。</summary>
    static PotController MakeAtIngredientResolve(out GameState state)
    {
        state = new GameState();
        var ctrl = new PotController(state);
        ctrl.StartPot();
        ctrl.StartBowl();
        ctrl.AdvanceBowlPhase(); // → Customer
        ctrl.AdvanceBowlPhase(); // → ItemPhase
        ctrl.AdvanceBowlPhase(); // → IngredientSelection
        ctrl.AdvanceBowlPhase(); // → IngredientResolve
        return ctrl;
    }

    static void AddIngredient(PotController ctrl, GameState state, IngredientDefinition def)
    {
        var inst = new IngredientInstance(def);
        // 手动将 BaseScore 和 Flavor 通过食材 Definition 注入（模拟 Effect 自动处理）
        // 但此处我们要走完整 Effect 路径，所以 Definition.Effects 必须包含相关效果
        // 用 EffectSystem 走真实路径
        var es = new EffectSystem();
        // 先注入 BaseScore 和 Flavor（食材进锅时，调用方负责应用 BaseScore 和基础 Flavor）
        // 当前架构：IngredientDefinition.BaseScore 和 Flavors 是数据，
        // AddIngredient 只触发 Effects 列表，不自动应用 BaseScore/Flavors。
        // 所以我们需要在 Effects 中包含 AddScore 和 AddFlavor，
        // 或者在测试中手动应用。
        // 检查：PotController.AddIngredient 只做 TriggerAll(ingredient.Definition.Effects, ...)
        // 不自动把 BaseScore/Flavors 加到 Pot — 这需要 Effects 包含相应操作。
        // IngredientData 的 Definition 中：
        //   米饭：flavors 有 Umami+1，但没有 AddFlavorEffect 在 effects 列表
        //   这意味着当前架构中 Flavor 和 BaseScore 需要通过 Effect 添加，
        //   OR 调用方在 AddIngredient 前后手动处理。
        // 设计文档期望食材 Effects 处理这些——所以我们需要在 Definition.Effects 中包含 AddFlavor 和 AddScore。
        // 但 IngredientData 目前没有这样做。
        // 结论：IngredientData 需要在 effects 中包含 AddScoreEffect + AddFlavorEffect。
        // 这个问题在设计时需要注意，见下面的架构注释。
        ctrl.AddIngredient(inst, es);
    }

    // ── Registry 完整性 ────────────────────────────────────────────────────────

    static void Test_Registry_HasTenIngredients()
    {
        Assert(IngredientData.Registry.GetAll().Count == 10,
            "正式 Registry 应包含 10 种食材");
    }

    static void Test_Registry_AllIdsUnique()
    {
        var ids = IngredientData.Registry.GetAll().Select(d => d.Id).ToList();
        Assert(ids.Count == ids.Distinct().Count(),
            "所有食材 ID 应唯一");
    }

    static void Test_Registry_CanGetByAllIds()
    {
        var ids = new[]
        {
            "rice", "sugar", "pepper", "red_date", "ginger",
            "vinegar", "honey", "chili_oil", "ice_cube", "egg"
        };
        foreach (var id in ids)
        {
            var def = IngredientData.Registry.Get(id);
            Assert(def.Id == id, $"Registry.Get(\"{id}\") 应返回正确 Definition");
        }
    }

    // ── 基础属性 ──────────────────────────────────────────────────────────────

    static void Test_Rice_Attributes()
    {
        var d = IngredientData.Rice;
        Assert(d.Id == "rice", "米饭 ID");
        Assert(d.Name == "米饭", "米饭 名称");
        Assert(d.Rarity == IngredientRarity.Common, "米饭 稀有度=普通");
        Assert(d.BaseScore == 1, "米饭 基础分=1");
        Assert(d.Flavors.ContainsKey(FlavorType.Umami) && d.Flavors[FlavorType.Umami] == 1, "米饭 鲜+1");
    }

    static void Test_Sugar_Attributes()
    {
        var d = IngredientData.Sugar;
        Assert(d.Id == "sugar", "糖 ID");
        Assert(d.Rarity == IngredientRarity.Common, "糖 稀有度=普通");
        Assert(d.BaseScore == 2, "糖 基础分=2");
        Assert(d.Flavors.ContainsKey(FlavorType.Sweet) && d.Flavors[FlavorType.Sweet] == 1, "糖 甜+1");
    }

    static void Test_Pepper_Attributes()
    {
        var d = IngredientData.Pepper;
        Assert(d.Id == "pepper", "辣椒 ID");
        Assert(d.Rarity == IngredientRarity.Common, "辣椒 稀有度=普通");
        Assert(d.BaseScore == 2, "辣椒 基础分=2");
        Assert(d.Flavors.ContainsKey(FlavorType.Spicy) && d.Flavors[FlavorType.Spicy] == 1, "辣椒 辣+1");
    }

    static void Test_RedDate_Attributes()
    {
        var d = IngredientData.RedDate;
        Assert(d.Id == "red_date", "红枣 ID");
        Assert(d.Rarity == IngredientRarity.Common, "红枣 稀有度=普通");
        Assert(d.BaseScore == 1, "红枣 基础分=1");
        Assert(d.Flavors.ContainsKey(FlavorType.Sweet) && d.Flavors[FlavorType.Sweet] == 1, "红枣 甜+1");
        Assert(d.Effects.Count >= 1, "红枣 应有条件效果");
    }

    static void Test_Ginger_Attributes()
    {
        var d = IngredientData.Ginger;
        Assert(d.Id == "ginger", "姜 ID");
        Assert(d.BaseScore == 1, "姜 基础分=1");
        Assert(d.Flavors.ContainsKey(FlavorType.Spicy) && d.Flavors[FlavorType.Spicy] == 1, "姜 辣+1");
    }

    static void Test_Vinegar_Attributes()
    {
        var d = IngredientData.Vinegar;
        Assert(d.Id == "vinegar", "醋 ID");
        Assert(d.BaseScore == 1, "醋 基础分=1");
        Assert(d.Flavors.ContainsKey(FlavorType.Sour) && d.Flavors[FlavorType.Sour] == 1, "醋 酸+1");
    }

    static void Test_Honey_Attributes()
    {
        var d = IngredientData.Honey;
        Assert(d.Id == "honey", "蜂蜜 ID");
        Assert(d.Rarity == IngredientRarity.Rare, "蜂蜜 稀有度=稀有");
        Assert(d.BaseScore == 2, "蜂蜜 基础分=2");
        Assert(d.Flavors.ContainsKey(FlavorType.Sweet) && d.Flavors[FlavorType.Sweet] == 1, "蜂蜜 甜+1");
    }

    static void Test_ChiliOil_Attributes()
    {
        var d = IngredientData.ChiliOil;
        Assert(d.Id == "chili_oil", "辣油 ID");
        Assert(d.Rarity == IngredientRarity.Rare, "辣油 稀有度=稀有");
        Assert(d.BaseScore == 2, "辣油 基础分=2");
        Assert(d.Flavors.ContainsKey(FlavorType.Spicy) && d.Flavors[FlavorType.Spicy] == 1, "辣油 辣+1");
    }

    static void Test_IceCube_Attributes()
    {
        var d = IngredientData.IceCube;
        Assert(d.Id == "ice_cube", "冰块 ID");
        Assert(d.Rarity == IngredientRarity.Rare, "冰块 稀有度=稀有");
        Assert(d.BaseScore == 0, "冰块 基础分=0");
        Assert(d.Flavors.Count == 0, "冰块 无味道");
        Assert(d.Effects.Count >= 1, "冰块 应有最终倍率效果");
    }

    static void Test_Egg_Attributes()
    {
        var d = IngredientData.Egg;
        Assert(d.Id == "egg", "鸡蛋 ID");
        Assert(d.Rarity == IngredientRarity.Common, "鸡蛋 稀有度=普通");
        Assert(d.BaseScore == 3, "鸡蛋 基础分=3");
        Assert(d.Flavors.ContainsKey(FlavorType.Umami) && d.Flavors[FlavorType.Umami] == 1, "鸡蛋 鲜+1");
    }

    // ── Instance 创建 ─────────────────────────────────────────────────────────

    static void Test_CreateInstance_ReferencesCorrectDefinition()
    {
        var inst = IngredientData.CreateInstance("rice");
        Assert(ReferenceEquals(inst.Definition, IngredientData.Rice),
            "CreateInstance(\"rice\") 应返回引用 IngredientData.Rice 的实例");
        Assert(inst.InstanceId.Length > 0, "InstanceId 应非空");
    }

    // ── 效果行为：红枣 ────────────────────────────────────────────────────────

    static void Test_RedDate_Effect_SweetGte3_AddsBonus()
    {
        var pot = new PotState();
        pot.AddFlavor(FlavorType.Sweet, 3); // 正好 >=3
        var ctx = MakeContext(pot);
        var es = new EffectSystem();

        es.TriggerAll(IngredientData.RedDate.Effects, "red_date_inst", ctx);

        Assert(pot.BaseScore == 2, "红枣：甜=3 时应 +2 分");
    }

    static void Test_RedDate_Effect_SweetLt3_NoBonus()
    {
        var pot = new PotState();
        pot.AddFlavor(FlavorType.Sweet, 2);
        var ctx = MakeContext(pot);
        var es = new EffectSystem();

        es.TriggerAll(IngredientData.RedDate.Effects, "red_date_inst", ctx);

        Assert(pot.BaseScore == 0, "红枣：甜<3 时不应加分");
    }

    // ── 效果行为：姜 ──────────────────────────────────────────────────────────

    static void Test_Ginger_Effect_SpicyGte3_AddsBonus()
    {
        var pot = new PotState();
        pot.AddFlavor(FlavorType.Spicy, 4);
        var ctx = MakeContext(pot);
        var es = new EffectSystem();

        es.TriggerAll(IngredientData.Ginger.Effects, "ginger_inst", ctx);

        Assert(pot.BaseScore == 2, "姜：辣>=3 时应 +2 分");
    }

    // ── 效果行为：醋 ──────────────────────────────────────────────────────────

    static void Test_Vinegar_Effect_SweetGte3_AddsBonus()
    {
        var pot = new PotState();
        pot.AddFlavor(FlavorType.Sweet, 3);
        var ctx = MakeContext(pot);
        var es = new EffectSystem();

        es.TriggerAll(IngredientData.Vinegar.Effects, "vinegar_inst", ctx);

        Assert(pot.BaseScore == 2, "醋：甜=3 时应 +2 分");
    }

    static void Test_Vinegar_Effect_Triggers_OnSweet_NotSour()
    {
        var pot = new PotState();
        pot.AddFlavor(FlavorType.Sour, 10); // 酸很高，但触发条件是甜
        pot.AddFlavor(FlavorType.Sweet, 1); // 甜不足3
        var ctx = MakeContext(pot);
        var es = new EffectSystem();

        es.TriggerAll(IngredientData.Vinegar.Effects, "vinegar_inst", ctx);

        Assert(pot.BaseScore == 0, "醋：触发条件是甜≥3，不是酸≥3；酸高甜低时不应加分");
    }

    // ── 效果行为：蜂蜜 ────────────────────────────────────────────────────────

    static void Test_Honey_Effect_Scaled_Sweet6()
    {
        var pot = new PotState();
        pot.AddFlavor(FlavorType.Sweet, 6); // 6/3=2 → +4
        var ctx = MakeContext(pot);
        var es = new EffectSystem();

        es.TriggerAll(IngredientData.Honey.Effects, "honey_inst", ctx);

        Assert(pot.BaseScore == 4, "蜂蜜：甜=6 时应 +4 分（2×2）");
    }

    static void Test_Honey_Effect_Scaled_Sweet9()
    {
        var pot = new PotState();
        pot.AddFlavor(FlavorType.Sweet, 9); // 9/3=3 → +6
        var ctx = MakeContext(pot);
        var es = new EffectSystem();

        es.TriggerAll(IngredientData.Honey.Effects, "honey_inst", ctx);

        Assert(pot.BaseScore == 6, "蜂蜜：甜=9 时应 +6 分（3×2）");
    }

    static void Test_Honey_Effect_Sweet0_NoBonus()
    {
        var pot = new PotState();
        var ctx = MakeContext(pot);
        var es = new EffectSystem();

        es.TriggerAll(IngredientData.Honey.Effects, "honey_inst", ctx);

        Assert(pot.BaseScore == 0, "蜂蜜：甜=0 时不应加分");
    }

    // ── 效果行为：辣油 ────────────────────────────────────────────────────────

    static void Test_ChiliOil_Effect_Scaled_Spicy6()
    {
        var pot = new PotState();
        pot.AddFlavor(FlavorType.Spicy, 6);
        var ctx = MakeContext(pot);
        var es = new EffectSystem();

        es.TriggerAll(IngredientData.ChiliOil.Effects, "chili_oil_inst", ctx);

        Assert(pot.BaseScore == 4, "辣油：辣=6 时应 +4 分（2×2）");
    }

    // ── 效果行为：冰块 ────────────────────────────────────────────────────────

    static void Test_IceCube_Effect_SetsFinalScoreMultiplier()
    {
        var pot = new PotState();
        var ctx = MakeContext(pot);
        var es = new EffectSystem();

        es.TriggerAll(IngredientData.IceCube.Effects, "ice_cube_inst", ctx);

        Assert(Math.Abs(pot.FinalScoreMultiplier - 1.5) < 1e-9,
            "冰块：FinalScoreMultiplier 应变为 1.5");
    }

    static void Test_IceCube_Effect_AppliedInCalculateAndLock()
    {
        var pot = new PotState { BowlNumber = 1, BaseScore = 10 };
        pot.FinalScoreMultiplier = 1.5;

        SevenSpices.Core.Scoring.ScoreCalculator.CalculateAndLock(pot);

        // 10 × 1 × 1.5 = 15
        Assert(pot.FinalScore == 15,
            "冰块效果下 BaseScore=10, Bowl1: FinalScore 应为 15");
    }

    // ── 效果行为：鸡蛋 ────────────────────────────────────────────────────────

    static void Test_Egg_Effect_ThreeUniqueIngredients_AddsBonus()
    {
        // 模拟锅中已有 3 种不同食材（含鸡蛋本身）
        var pot = new PotState();
        pot.Ingredients.Add(new IngredientInstance(IngredientData.Rice));
        pot.Ingredients.Add(new IngredientInstance(IngredientData.Sugar));
        pot.Ingredients.Add(new IngredientInstance(IngredientData.Egg)); // 第3种
        var ctx = MakeContext(pot);
        var es = new EffectSystem();

        es.TriggerAll(IngredientData.Egg.Effects, "egg_inst", ctx);

        Assert(pot.BaseScore == 3, "鸡蛋：锅中已有3种不同食材时应 +3 分");
    }

    static void Test_Egg_Effect_TwoUniqueIngredients_NoBonus()
    {
        var pot = new PotState();
        pot.Ingredients.Add(new IngredientInstance(IngredientData.Rice));
        pot.Ingredients.Add(new IngredientInstance(IngredientData.Egg)); // 仅2种
        var ctx = MakeContext(pot);
        var es = new EffectSystem();

        es.TriggerAll(IngredientData.Egg.Effects, "egg_inst", ctx);

        Assert(pot.BaseScore == 0, "鸡蛋：锅中只有2种不同食材时不应加分");
    }

    static void Test_Egg_Effect_CountsByDefinitionNotInstance()
    {
        // 3个米饭实例 + 鸡蛋 = 只有2种 Definition，不触发
        var pot = new PotState();
        pot.Ingredients.Add(new IngredientInstance(IngredientData.Rice));
        pot.Ingredients.Add(new IngredientInstance(IngredientData.Rice)); // 同种第2份
        pot.Ingredients.Add(new IngredientInstance(IngredientData.Rice)); // 同种第3份
        pot.Ingredients.Add(new IngredientInstance(IngredientData.Egg));
        var ctx = MakeContext(pot);
        var es = new EffectSystem();

        es.TriggerAll(IngredientData.Egg.Effects, "egg_inst", ctx);

        Assert(pot.BaseScore == 0, "鸡蛋：按 Definition 种类去重，3份米饭+鸡蛋只有2种");
    }

    // ── 辅助私有 ──────────────────────────────────────────────────────────────

    static EffectContext MakeContext(PotState pot)
    {
        var state = new GameState();
        return new EffectContext(1, state, pot);
    }

    static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception($"[FAIL] IngredientDataTests: {message}");
    }
}
