using SevenSpices.Core.Bottom;
using SevenSpices.Core.Game;

namespace SevenSpices.Tests.Bottom;

public static class BottomExtractorTests
{
    public static void RunAll()
    {
        Test_Extract_20_Gives_6();
        Test_Extract_26_Gives_7_Floor();
        Test_Extract_3_Gives_1_MinimumOne();
        Test_Extract_1_Gives_1_MinimumOne();
        Test_Extract_0_DoesNotWrite();
        Test_Extract_MultipleFlavors_IndependentCalculation();
        Test_Extract_UnappearedFlavor_KeepsOldBottom();
        Test_Extract_SmallNewValue_DoesNotReduceBottom();
        Test_Extract_DoesNotModifyPot();
        Test_Extract_ThenApplyToPot_Integration();

        Console.WriteLine("All BottomExtractorTests passed.");
    }

    static PotState MakePot(FlavorType flavor, int value)
    {
        var pot = new PotState();
        pot.AddFlavor(flavor, value);
        return pot;
    }

    // --- 基础提炼 ---

    static void Test_Extract_20_Gives_6()
    {
        var pot = MakePot(FlavorType.Sweet, 20);
        var bottom = new BottomState();
        BottomExtractor.Extract(pot, bottom);
        Assert(bottom.GetFlavor(FlavorType.Sweet) == 6, "20 × 30% = 6.0 → 6");
    }

    static void Test_Extract_26_Gives_7_Floor()
    {
        // 26 × 30% = 7.8 → Floor → 7，验证使用 Floor 而非四舍五入
        var pot = MakePot(FlavorType.Sweet, 26);
        var bottom = new BottomState();
        BottomExtractor.Extract(pot, bottom);
        Assert(bottom.GetFlavor(FlavorType.Sweet) == 7, "26 × 30% = 7.8 → Floor → 7 (not rounded to 8)");
    }

    // --- 最低保留 1 ---

    static void Test_Extract_3_Gives_1_MinimumOne()
    {
        // 3 × 30% = 0.9 → Floor → 0 → Max(1, 0) → 1
        var pot = MakePot(FlavorType.Spicy, 3);
        var bottom = new BottomState();
        BottomExtractor.Extract(pot, bottom);
        Assert(bottom.GetFlavor(FlavorType.Spicy) == 1, "3 × 30% = 0.9 → Floor → 0 → minimum 1");
    }

    static void Test_Extract_1_Gives_1_MinimumOne()
    {
        var pot = MakePot(FlavorType.Sour, 1);
        var bottom = new BottomState();
        BottomExtractor.Extract(pot, bottom);
        Assert(bottom.GetFlavor(FlavorType.Sour) == 1, "1 × 30% → minimum 1");
    }

    static void Test_Extract_0_DoesNotWrite()
    {
        // Flavor 未设置即为 0，说明该味道本锅从未出现 → 不写入锅底
        var pot = new PotState();
        var bottom = new BottomState();
        BottomExtractor.Extract(pot, bottom);
        Assert(bottom.GetFlavor(FlavorType.Umami) == 0, "0 → 不写入锅底（不是 minimum 1）");
    }

    // --- 多种 Flavor 独立计算 ---

    static void Test_Extract_MultipleFlavors_IndependentCalculation()
    {
        var pot = new PotState();
        pot.AddFlavor(FlavorType.Sweet, 20);   // → 6
        pot.AddFlavor(FlavorType.Spicy, 10);   // → 3
        pot.AddFlavor(FlavorType.Sour, 3);     // → 1 (minimum)
        // Bitter / Umami 未出现 → 不写入（保持 0）

        var bottom = new BottomState();
        BottomExtractor.Extract(pot, bottom);

        Assert(bottom.GetFlavor(FlavorType.Sweet) == 6, "Sweet 20 → 6");
        Assert(bottom.GetFlavor(FlavorType.Spicy) == 3, "Spicy 10 → 3");
        Assert(bottom.GetFlavor(FlavorType.Sour) == 1, "Sour 3 → 1 (minimum)");
        Assert(bottom.GetFlavor(FlavorType.Bitter) == 0, "Bitter 未出现 → 不写入");
        Assert(bottom.GetFlavor(FlavorType.Umami) == 0, "Umami 未出现 → 不写入");
    }

    // --- 单调不减：本锅未出现的味道保留旧锅底 ---

    static void Test_Extract_UnappearedFlavor_KeepsOldBottom()
    {
        var pot = MakePot(FlavorType.Sweet, 20); // 本锅只出现甜味
        var bottom = new BottomState();
        bottom.SetFlavor(FlavorType.Spicy, 6);   // 旧锅底有 6 级辣

        BottomExtractor.Extract(pot, bottom);

        Assert(bottom.GetFlavor(FlavorType.Spicy) == 6,
            "本锅未出现辣味时，旧锅底的辣味不应被清零");
    }

    // --- 单调不减：小量新增时锅底不下降 ---

    static void Test_Extract_SmallNewValue_DoesNotReduceBottom()
    {
        // 旧锅底 6，本锅新增 1 → 本锅最终 7 → 公式值 max(1, floor(7×0.3)) = 2
        // 2 < 6，锅底应保留 6，而不是下降到 2
        var pot = MakePot(FlavorType.Sweet, 7);
        var bottom = new BottomState();
        bottom.SetFlavor(FlavorType.Sweet, 6);

        BottomExtractor.Extract(pot, bottom);

        Assert(bottom.GetFlavor(FlavorType.Sweet) == 6,
            "小量新增时锅底单调不减：应保留旧值 6，而不是下降到 2");
    }

    // --- Pot 不被修改 ---

    static void Test_Extract_DoesNotModifyPot()
    {
        var pot = MakePot(FlavorType.Sweet, 20);
        var bottom = new BottomState();

        BottomExtractor.Extract(pot, bottom);

        Assert(pot.GetFlavor(FlavorType.Sweet) == 20, "PotState must not be modified by Extract");
    }

    // --- ApplyToPot 集成 ---

    static void Test_Extract_ThenApplyToPot_Integration()
    {
        // 模拟设计文档中的滚雪球示例：第1锅 20甜 → 提炼 6 → 下锅起始 6
        var oldPot = new PotState();
        oldPot.AddFlavor(FlavorType.Sweet, 20);

        var bottom = new BottomState();
        BottomExtractor.Extract(oldPot, bottom);
        Assert(bottom.GetFlavor(FlavorType.Sweet) == 6, "Extract: 20 → 6");

        var newPot = new PotState();
        bottom.ApplyToPot(newPot);
        Assert(newPot.GetFlavor(FlavorType.Sweet) == 6,
            "ApplyToPot: new pot must start with 6 sweet from bottom");
    }

    static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception($"[FAIL] {message}");
    }
}
