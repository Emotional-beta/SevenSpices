using SevenSpices.Tests.Bottom;
using SevenSpices.Tests.Customers;
using SevenSpices.Tests.Effects;
using SevenSpices.Tests.Game;
using SevenSpices.Tests.Ingredients;
using SevenSpices.Tests.Items;
using SevenSpices.Tests.Pot;
using SevenSpices.Tests.Scoring;

namespace SevenSpices.Tests;

/// <summary>
/// Phase 1 &amp; Phase 2 &amp; Phase 3 核心模拟器测试入口。
/// </summary>
public static class TestRunner
{
    public static void RunAll()
    {
        IngredientPoolTests.RunAll();
        IngredientDefinitionTests.RunAll();
        IngredientRegistryTests.RunAll();
        GameStateTests.RunAll();
        EffectSystemTests.RunAll();
        PotControllerTests.RunAll();
        CustomerDefinitionTests.RunAll();
        SatisfactionEvaluatorTests.RunAll();
        CustomerInstanceTests.RunAll();
        CustomerRegistryTests.RunAll();
        ItemDefinitionTests.RunAll();
        ItemInstanceTests.RunAll();
        ItemRegistryTests.RunAll();
        ScoreCalculatorTests.RunAll();
        BottomExtractorTests.RunAll();
        Console.WriteLine("=== All tests passed. ===");
    }
}
