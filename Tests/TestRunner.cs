using SevenSpices.Tests.Effects;
using SevenSpices.Tests.Game;
using SevenSpices.Tests.Ingredients;
using SevenSpices.Tests.Pot;

namespace SevenSpices.Tests;

/// <summary>
/// Phase 1 核心模拟器测试入口。
/// 在没有 UI 的情况下验证基础数据结构是否正确。
/// </summary>
public static class TestRunner
{
    public static void RunAll()
    {
        IngredientPoolTests.RunAll();
        GameStateTests.RunAll();
        EffectSystemTests.RunAll();
        PotControllerTests.RunAll();
        Console.WriteLine("=== All Phase 1 tests passed. ===");
    }
}
