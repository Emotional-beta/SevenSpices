using SevenSpices.Core.Game;
using SevenSpices.Core.Scoring;

namespace SevenSpices.Tests.Scoring;

public static class ScoreCalculatorTests
{
    public static void RunAll()
    {
        Test_GetMultiplier_Bowl1();
        Test_GetMultiplier_Bowl2();
        Test_GetMultiplier_Bowl3();
        Test_GetMultiplier_Bowl4();
        Test_GetMultiplier_Bowl5();
        Test_GetMultiplier_Bowl6();
        Test_GetMultiplier_Bowl7();
        Test_GetMultiplier_Bowl8();
        Test_GetMultiplier_Bowl9();
        Test_GetMultiplier_Bowl10();
        Test_GetMultiplier_Bowl11();
        Test_GetMultiplier_Bowl12();
        Test_GetMultiplier_Bowl20();
        Test_GetMultiplier_Bowl100();
        Test_GetMultiplier_BowlMaxValue();
        Test_GetMultiplier_Zero_Throws();
        Test_GetMultiplier_Negative_Throws();
        Test_CalculateAndLock_Bowl7_BaseScore100();
        Test_CalculateAndLock_Bowl10_BaseScore100();
        Test_CalculateAndLock_Bowl11_BaseScore100();
        Test_CalculateAndLock_BaseScore0();
        Test_CalculateAndLock_SetsIsScoreLocked();
        Test_CalculateAndLock_DoesNotModifyBaseScore();
        Test_CalculateAndLock_AlreadyLocked_Throws();
        Test_CalculateAndLock_AlreadyLocked_FinalScoreUnchanged();

        Console.WriteLine("All ScoreCalculatorTests passed.");
    }

    static PotState MakePot(int bowlNumber, int baseScore) =>
        new() { BowlNumber = bowlNumber, BaseScore = baseScore };

    // --- GetMultiplier: 完整倍率表 ---

    static void Test_GetMultiplier_Bowl1() =>
        Assert(ScoreCalculator.GetMultiplier(1) == 1, "Bowl 1 must be ×1");

    static void Test_GetMultiplier_Bowl2() =>
        Assert(ScoreCalculator.GetMultiplier(2) == 1, "Bowl 2 must be ×1");

    static void Test_GetMultiplier_Bowl3() =>
        Assert(ScoreCalculator.GetMultiplier(3) == 1, "Bowl 3 must be ×1");

    static void Test_GetMultiplier_Bowl4() =>
        Assert(ScoreCalculator.GetMultiplier(4) == 1, "Bowl 4 must be ×1");

    static void Test_GetMultiplier_Bowl5() =>
        Assert(ScoreCalculator.GetMultiplier(5) == 1, "Bowl 5 must be ×1");

    static void Test_GetMultiplier_Bowl6() =>
        Assert(ScoreCalculator.GetMultiplier(6) == 2, "Bowl 6 must be ×2");

    static void Test_GetMultiplier_Bowl7() =>
        Assert(ScoreCalculator.GetMultiplier(7) == 4, "Bowl 7 must be ×4");

    static void Test_GetMultiplier_Bowl8() =>
        Assert(ScoreCalculator.GetMultiplier(8) == 8, "Bowl 8 must be ×8");

    static void Test_GetMultiplier_Bowl9() =>
        Assert(ScoreCalculator.GetMultiplier(9) == 16, "Bowl 9 must be ×16");

    static void Test_GetMultiplier_Bowl10() =>
        Assert(ScoreCalculator.GetMultiplier(10) == 32, "Bowl 10 must be ×32");

    // --- GetMultiplier: 最终锅超过 10 碗 ---

    static void Test_GetMultiplier_Bowl11() =>
        Assert(ScoreCalculator.GetMultiplier(11) == 32, "Bowl 11 must be ×32 (final pot)");

    static void Test_GetMultiplier_Bowl12() =>
        Assert(ScoreCalculator.GetMultiplier(12) == 32, "Bowl 12 must be ×32 (final pot)");

    static void Test_GetMultiplier_Bowl20() =>
        Assert(ScoreCalculator.GetMultiplier(20) == 32, "Bowl 20 must be ×32 (final pot)");

    static void Test_GetMultiplier_Bowl100() =>
        Assert(ScoreCalculator.GetMultiplier(100) == 32, "Bowl 100 must be ×32 (final pot)");

    static void Test_GetMultiplier_BowlMaxValue() =>
        Assert(ScoreCalculator.GetMultiplier(int.MaxValue) == 32, "int.MaxValue bowl must be ×32 (final pot)");

    // --- GetMultiplier: 非法碗数 ---

    static void Test_GetMultiplier_Zero_Throws()
    {
        try
        {
            ScoreCalculator.GetMultiplier(0);
            Assert(false, "BowlNumber 0 must throw ArgumentOutOfRangeException");
        }
        catch (ArgumentOutOfRangeException) { }
    }

    static void Test_GetMultiplier_Negative_Throws()
    {
        try
        {
            ScoreCalculator.GetMultiplier(-1);
            Assert(false, "Negative BowlNumber must throw ArgumentOutOfRangeException");
        }
        catch (ArgumentOutOfRangeException) { }
    }

    // --- CalculateAndLock ---

    static void Test_CalculateAndLock_Bowl7_BaseScore100()
    {
        var pot = MakePot(7, 100);
        ScoreCalculator.CalculateAndLock(pot);
        Assert(pot.FinalScore == 400, "Bowl 7, BaseScore 100 → FinalScore must be 400");
    }

    static void Test_CalculateAndLock_Bowl10_BaseScore100()
    {
        var pot = MakePot(10, 100);
        ScoreCalculator.CalculateAndLock(pot);
        Assert(pot.FinalScore == 3200, "Bowl 10, BaseScore 100 → FinalScore must be 3200");
    }

    static void Test_CalculateAndLock_Bowl11_BaseScore100()
    {
        var pot = MakePot(11, 100);
        ScoreCalculator.CalculateAndLock(pot);
        Assert(pot.FinalScore == 3200, "Bowl 11, BaseScore 100 → FinalScore must be 3200 (final pot uses ×32)");
    }

    static void Test_CalculateAndLock_BaseScore0()
    {
        var pot = MakePot(7, 0);
        ScoreCalculator.CalculateAndLock(pot);
        Assert(pot.FinalScore == 0, "BaseScore 0 → FinalScore must be 0");
        Assert(pot.IsScoreLocked, "IsScoreLocked must be true even when FinalScore is 0");
    }

    static void Test_CalculateAndLock_SetsIsScoreLocked()
    {
        var pot = MakePot(1, 50);
        Assert(!pot.IsScoreLocked, "IsScoreLocked must be false before CalculateAndLock");
        ScoreCalculator.CalculateAndLock(pot);
        Assert(pot.IsScoreLocked, "IsScoreLocked must be true after CalculateAndLock");
    }

    static void Test_CalculateAndLock_DoesNotModifyBaseScore()
    {
        var pot = MakePot(6, 100);
        ScoreCalculator.CalculateAndLock(pot);
        Assert(pot.BaseScore == 100, "BaseScore must not be modified by CalculateAndLock");
    }

    static void Test_CalculateAndLock_AlreadyLocked_Throws()
    {
        var pot = MakePot(5, 100);
        ScoreCalculator.CalculateAndLock(pot);

        try
        {
            ScoreCalculator.CalculateAndLock(pot);
            Assert(false, "Second CalculateAndLock on locked pot must throw InvalidOperationException");
        }
        catch (InvalidOperationException) { }
    }

    static void Test_CalculateAndLock_AlreadyLocked_FinalScoreUnchanged()
    {
        var pot = MakePot(5, 100);
        ScoreCalculator.CalculateAndLock(pot);
        int firstFinalScore = pot.FinalScore;

        pot.BaseScore = 999;
        try { ScoreCalculator.CalculateAndLock(pot); } catch (InvalidOperationException) { }

        Assert(pot.FinalScore == firstFinalScore, "FinalScore must not change after failed second CalculateAndLock");
        Assert(pot.IsScoreLocked, "IsScoreLocked must remain true");
    }

    static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception($"[FAIL] {message}");
    }
}
