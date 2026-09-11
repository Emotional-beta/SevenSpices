using SevenSpices.Core.Effects;
using SevenSpices.Core.Ingredients;
using SevenSpices.Tests.Effects;

namespace SevenSpices.Tests.Ingredients;

public static class IngredientDefinitionTests
{
    public static void RunAll()
    {
        Test_NoEffects_ReturnsEmptyList();
        Test_NullEffects_ReturnsEmptyList();
        Test_Effects_CountCorrect();
        Test_Effects_OrderPreserved();
        Test_Effects_SameReferenceStored();
        Test_Effects_IsReadOnly();
        Test_ExistingFields_Unchanged();

        Console.WriteLine("All IngredientDefinitionTests passed.");
    }

    static IEffect MakeEffect(string id) =>
        new LambdaEffect(id, _ => { });

    static void Test_NoEffects_ReturnsEmptyList()
    {
        var def = new IngredientDefinition("rice", "米饭", IngredientRarity.Common, 5);
        Assert(def.Effects.Count == 0, "Effects must be empty when not provided");
    }

    static void Test_NullEffects_ReturnsEmptyList()
    {
        var def = new IngredientDefinition("rice", "米饭", IngredientRarity.Common, 5, null, null);
        Assert(def.Effects.Count == 0, "Effects must be empty when null is passed");
    }

    static void Test_Effects_CountCorrect()
    {
        var effects = new[] { MakeEffect("e1"), MakeEffect("e2"), MakeEffect("e3") };
        var def = new IngredientDefinition("rice", "米饭", IngredientRarity.Common, 5, effects: effects);
        Assert(def.Effects.Count == 3, "Effects count must match the number passed in");
    }

    static void Test_Effects_OrderPreserved()
    {
        var e1 = MakeEffect("e1");
        var e2 = MakeEffect("e2");
        var e3 = MakeEffect("e3");
        var def = new IngredientDefinition("rice", "米饭", IngredientRarity.Common, 5, effects: new[] { e1, e2, e3 });
        Assert(def.Effects[0].EffectId == "e1", "First effect must be e1");
        Assert(def.Effects[1].EffectId == "e2", "Second effect must be e2");
        Assert(def.Effects[2].EffectId == "e3", "Third effect must be e3");
    }

    static void Test_Effects_SameReferenceStored()
    {
        var e1 = MakeEffect("e1");
        var e2 = MakeEffect("e2");
        var def = new IngredientDefinition("rice", "米饭", IngredientRarity.Common, 5, effects: new[] { e1, e2 });
        Assert(ReferenceEquals(def.Effects[0], e1), "Effects[0] must be the same object reference");
        Assert(ReferenceEquals(def.Effects[1], e2), "Effects[1] must be the same object reference");
    }

    static void Test_Effects_IsReadOnly()
    {
        var def = new IngredientDefinition("rice", "米饭", IngredientRarity.Common, 5,
            effects: new[] { MakeEffect("e1") });

        try
        {
            ((System.Collections.Generic.IList<IEffect>)def.Effects).Add(MakeEffect("e2"));
            Assert(false, "Effects must be read-only");
        }
        catch (NotSupportedException) { }

        Assert(def.Effects.Count == 1, "Internal collection must not be modified via Effects");
    }

    static void Test_ExistingFields_Unchanged()
    {
        var def = new IngredientDefinition("rice", "米饭", IngredientRarity.Common, 5);
        Assert(def.Id == "rice", "Id must be unchanged");
        Assert(def.Name == "米饭", "Name must be unchanged");
        Assert(def.Rarity == IngredientRarity.Common, "Rarity must be unchanged");
        Assert(def.BaseScore == 5, "BaseScore must be unchanged");
    }

    static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception($"[FAIL] {message}");
    }
}
