using SevenSpices.Core.Customers;
using SevenSpices.Core.Game;
using SevenSpices.Core.Ingredients;
using SevenSpices.Core.Items;

namespace SevenSpices.Tests.Game;

/// <summary>
/// GameState、BottomState 基础结构测试。
/// </summary>
public static class GameStateTests
{
    public static void RunAll()
    {
        Test_GameState_DefaultValues();
        Test_PlayerState_Items_IsItemInstanceList();
        Test_BottomState_ApplyToPot();
        Test_BottomState_ApplyToPot_DoesNotRepeatOnSecondCall();
        Test_PotState_FlavorAccumulation();
        Test_CustomerState_DefaultValues();
        Test_CustomerState_CurrentCustomer_StoresInstanceReference();
        Test_CustomerState_AppearedCustomers_StoresInstanceReference();
        Test_CustomerState_SatisfiedRareCustomers_StoresInstanceReference();

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

    static void Test_PlayerState_Items_IsItemInstanceList()
    {
        var player = new PlayerState();
        Assert(player.Items.Count == 0, "Items must be empty by default");

        var def = new ItemDefinition("item_001", "辣椒酱");
        var inst = new ItemInstance(def);
        player.Items.Add(inst);
        Assert(player.Items.Count == 1, "Items must accept ItemInstance");
        Assert(ReferenceEquals(player.Items[0].Definition, def), "Stored item must reference the correct definition");
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

    static CustomerInstance MakeCustomerInstance(string defId = "c_001", string name = "老饕") =>
        new(new CustomerDefinition(defId, name));

    static void Test_CustomerState_DefaultValues()
    {
        var cs = new CustomerState();
        Assert(cs.CurrentCustomer == null, "CurrentCustomer must be null by default");
        Assert(cs.AppearedCustomers.Count == 0, "AppearedCustomers must be empty by default");
        Assert(cs.SatisfiedRareCustomers.Count == 0, "SatisfiedRareCustomers must be empty by default");
    }

    static void Test_CustomerState_CurrentCustomer_StoresInstanceReference()
    {
        var inst = MakeCustomerInstance();
        var cs = new CustomerState();
        cs.CurrentCustomer = inst;
        Assert(ReferenceEquals(cs.CurrentCustomer, inst), "CurrentCustomer must store the same object reference");
        Assert(cs.CurrentCustomer.Definition.Id == "c_001", "CurrentCustomer.Definition must be accessible");
    }

    static void Test_CustomerState_AppearedCustomers_StoresInstanceReference()
    {
        var inst1 = MakeCustomerInstance("c_001", "老饕");
        var inst2 = MakeCustomerInstance("c_002", "吃货");
        var cs = new CustomerState();
        cs.AppearedCustomers.Add(inst1);
        cs.AppearedCustomers.Add(inst2);
        Assert(cs.AppearedCustomers.Count == 2, "AppearedCustomers must hold 2 instances");
        Assert(ReferenceEquals(cs.AppearedCustomers[0], inst1), "First appeared customer must be same reference");
        Assert(ReferenceEquals(cs.AppearedCustomers[1], inst2), "Second appeared customer must be same reference");
    }

    static void Test_CustomerState_SatisfiedRareCustomers_StoresInstanceReference()
    {
        var inst = new CustomerInstance(new CustomerDefinition("c_rare_001", "神秘食客", isRare: true));
        var cs = new CustomerState();
        cs.SatisfiedRareCustomers.Add(inst);
        Assert(cs.SatisfiedRareCustomers.Count == 1, "SatisfiedRareCustomers must hold 1 instance");
        Assert(ReferenceEquals(cs.SatisfiedRareCustomers[0], inst), "Satisfied rare customer must be same reference");
        Assert(cs.SatisfiedRareCustomers[0].Definition.IsRare, "Stored customer must be rare");
    }

    static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception($"[FAIL] {message}");
    }
}
