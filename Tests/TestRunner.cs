using SevenSpices.Tests.Bottom;
using SevenSpices.Tests.Companions;
using SevenSpices.Tests.Content;
using SevenSpices.Tests.Customers;
using SevenSpices.Tests.Effects;
using SevenSpices.Tests.Events;
using SevenSpices.Tests.Flavors;
using SevenSpices.Tests.Game;
using SevenSpices.Tests.Ingredients;
using SevenSpices.Tests.Items;
using SevenSpices.Tests.Pot;
using SevenSpices.Tests.Run;
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
        GameControllerTests.RunAll();
        GameControllerEventTests.RunAll();
        ItemFlowTests.RunAll();
        CustomerFlowTests.RunAll();
        RunProgressionTests.RunAll();
        PotRewardTests.RunAll();
        ShopTests.RunAll();
        EffectSystemTests.RunAll();
        EventBusTests.RunAll();
        PotControllerTests.RunAll();
        PotLifecycleTests.RunAll();
        PreviewIngredientTests.RunAll();
        CustomerDefinitionTests.RunAll();
        SatisfactionEvaluatorTests.RunAll();
        CustomerInstanceTests.RunAll();
        CustomerRegistryTests.RunAll();
        CustomerServiceTests.RunAll();
        ItemDefinitionTests.RunAll();
        ItemInstanceTests.RunAll();
        ItemRegistryTests.RunAll();
        ScoreCalculatorTests.RunAll();
        FlavorScoreTests.RunAll();
        FlavorVerbTests.RunAll();
        ComplexVerbTests.RunAll();
        FlavorEntropyTests.RunAll();
        PhysicalStateTests.RunAll();
        BottomExtractorTests.RunAll();
        RunControllerTests.RunAll();
        FinalPotSettlementTests.RunAll();
        BossSettlementTests.RunAll();
        IngredientDataTests.RunAll();
        FullCoreRunTests.RunAll();
        CompanionSystemTests.RunAll();
        CompanionDataTests.RunAll();
        CompanionFlowTests.RunAll();
        PotEndSequenceTests.RunAll();
        Console.WriteLine("=== All tests passed. ===");
    }
}
