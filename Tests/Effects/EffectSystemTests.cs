using SevenSpices.Core.Effects;
using SevenSpices.Core.Game;
using SevenSpices.Core.Ingredients;

namespace SevenSpices.Tests.Effects;

/// <summary>
/// Effect 系统基础测试：执行、防循环、ChainId 独立性。
/// </summary>
public static class EffectSystemTests
{
    public static void RunAll()
    {
        Test_Effect_CanBeApplied();
        Test_SameSource_NotTriggeredTwiceInSameChain();
        Test_DifferentSources_BothTriggerInSameChain();
        Test_NewContext_AllowsRetriggerOfSameSource();
        Test_ChainIds_AreUnique();
        Test_TriggeredEffects_AreRecorded();
        Test_TriggerAll_SkipsAlreadyTriggeredSource();

        Console.WriteLine("All EffectSystemTests passed.");
    }

    // ── 辅助 ────────────────────────────────────────────────────────────────

    static EffectContext MakeContext(GameState? state = null, int bowl = 1,
        IngredientInstance? ingredient = null)
    {
        state ??= new GameState();
        return new EffectContext(bowl, state, state.Pot, ingredient);
    }

    static EffectSystem MakeSystem() => new();

    static IngredientDefinition MakeDef() =>
        new("rice", "米饭", IngredientRarity.Common, 5);

    // ── 测试 ────────────────────────────────────────────────────────────────

    static void Test_Effect_CanBeApplied()
    {
        int callCount = 0;
        var effect = new LambdaEffect("test_apply", _ => callCount++);
        var ctx = MakeContext();
        var sys = MakeSystem();

        sys.Trigger(effect, "source_a", ctx);

        Assert(callCount == 1, "Effect.Apply should be called once");
    }

    static void Test_SameSource_NotTriggeredTwiceInSameChain()
    {
        int callCount = 0;
        var effect = new LambdaEffect("test_loop", _ => callCount++);
        var ctx = MakeContext();
        var sys = MakeSystem();

        sys.Trigger(effect, "source_a", ctx);
        sys.Trigger(effect, "source_a", ctx); // 同一 sourceId，应被跳过

        Assert(callCount == 1, "Same source in same chain should only trigger once");
    }

    static void Test_DifferentSources_BothTriggerInSameChain()
    {
        int callCount = 0;
        var effect = new LambdaEffect("test_multi", _ => callCount++);
        var ctx = MakeContext();
        var sys = MakeSystem();

        sys.Trigger(effect, "source_a", ctx);
        sys.Trigger(effect, "source_b", ctx); // 不同 sourceId，都应触发

        Assert(callCount == 2, "Different sources in same chain should both trigger");
    }

    static void Test_NewContext_AllowsRetriggerOfSameSource()
    {
        int callCount = 0;
        var effect = new LambdaEffect("test_reuse", _ => callCount++);
        var state = new GameState();
        var sys = MakeSystem();

        var ctx1 = MakeContext(state);
        sys.Trigger(effect, "source_a", ctx1);

        var ctx2 = MakeContext(state); // 新的效果链
        sys.Trigger(effect, "source_a", ctx2);

        Assert(callCount == 2, "Same source in new chain should trigger again");
    }

    static void Test_ChainIds_AreUnique()
    {
        var state = new GameState();
        var ctx1 = MakeContext(state);
        var ctx2 = MakeContext(state);
        var ctx3 = MakeContext(state);

        Assert(ctx1.ChainId != ctx2.ChainId, "ChainId must be unique (ctx1 vs ctx2)");
        Assert(ctx2.ChainId != ctx3.ChainId, "ChainId must be unique (ctx2 vs ctx3)");
        Assert(ctx1.ChainId != ctx3.ChainId, "ChainId must be unique (ctx1 vs ctx3)");
    }

    static void Test_TriggeredEffects_AreRecorded()
    {
        var effectA = new LambdaEffect("effect_a", _ => { });
        var effectB = new LambdaEffect("effect_b", _ => { });
        var ctx = MakeContext();
        var sys = MakeSystem();

        sys.Trigger(effectA, "source_a", ctx);
        sys.Trigger(effectB, "source_b", ctx);

        Assert(ctx.TriggeredEffects.Contains("effect_a"), "effect_a should be recorded");
        Assert(ctx.TriggeredEffects.Contains("effect_b"), "effect_b should be recorded");
        Assert(ctx.TriggeredEffects.Count == 2, "Exactly 2 effects should be recorded");
    }

    static void Test_TriggerAll_SkipsAlreadyTriggeredSource()
    {
        int callCount = 0;
        var effects = new[]
        {
            new LambdaEffect("effect_x1", _ => callCount++),
            new LambdaEffect("effect_x2", _ => callCount++),
        };
        var ctx = MakeContext();
        var sys = MakeSystem();

        sys.Trigger(new LambdaEffect("pre", _ => { }), "source_x", ctx); // 先触发一次
        sys.TriggerAll(effects, "source_x", ctx); // 同一 source，应全部跳过

        Assert(callCount == 0, "TriggerAll with already-triggered source should skip all effects");
    }

    static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception($"[FAIL] {message}");
    }
}
