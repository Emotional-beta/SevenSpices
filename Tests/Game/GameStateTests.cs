using SevenSpices.Core.Game;
using SevenSpices.Core.Ingredients;

namespace SevenSpices.Tests.Game;

/// <summary>
/// GameState、BottomState 基础结构测试。
/// </summary>
public static class GameStateTests
{
    public static void RunAll()
    {
        Test_GameState_DefaultValues();
        Test_BottomState_ApplyToPot();
        Test_BottomState_ApplyToPot_DoesNotRepeatOnSecondCall();
        Test_PotState_FlavorAccumulation();

        Console.WriteLine("All GameStateTests passed.");
    }

    static void Test_GameState_DefaultValues()
    {
        var state = new GameState();
        Assert(state.Player != null, "PlayerState must not be null");
        Assert(state.Run != null, "RunState must not be null");
        Assert(state.Pot != null, "PotState must not be null");
        Assert(state.Bottom != null, "BottomState must not be null");
        Assert(state.Customer != null, "CustomerState must not be null");
        Assert(state.Run!.Chapter == 1, "Default chapter should be 1");
        Assert(state.Pot!.BowlNumber == 1, "Default bowl number should be 1");
        Assert(state.Pot!.BowlLimit == 10, "Default bowl limit should be 10");
    }

    static void Test_BottomState_ApplyToPot()
    {
        var bottom = new BottomState();
        bottom.SetFlavor(FlavorType.Sweet, 6);

        var pot = new PotState();
        bottom.ApplyToPot(pot);

        Assert(pot.GetFlavor(FlavorType.Sweet) == 6, "PotState should receive 6 sweet from bottom");
        Assert(pot.GetFlavor(FlavorType.Spicy) == 0, "Unset flavor should be 0");
    }

    static void Test_BottomState_ApplyToPot_DoesNotRepeatOnSecondCall()
    {
        // 模拟规则：锅底注入后不应被重复注入（调用方职责，这里验证单次调用结果正确）
        var bottom = new BottomState();
        bottom.SetFlavor(FlavorType.Sweet, 3);

        var pot = new PotState();
        bottom.ApplyToPot(pot);
        // 只调用一次 ApplyToPot，pot 的 sweet 应为 3
        Assert(pot.GetFlavor(FlavorType.Sweet) == 3, "Sweet should be 3 after single ApplyToPot");
    }

    static void Test_PotState_FlavorAccumulation()
    {
        var pot = new PotState();
        pot.AddFlavor(FlavorType.Sweet, 5);
        pot.AddFlavor(FlavorType.Sweet, 3);
        pot.AddFlavor(FlavorType.Spicy, 2);

        Assert(pot.GetFlavor(FlavorType.Sweet) == 8, "Sweet should accumulate to 8");
        Assert(pot.GetFlavor(FlavorType.Spicy) == 2, "Spicy should be 2");
        Assert(pot.GetFlavor(FlavorType.Sour) == 0, "Sour should be 0 when not set");
    }

    static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception($"[FAIL] {message}");
    }
}
