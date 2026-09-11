using SevenSpices.Core.Game;
using SevenSpices.Core.Ingredients;
using SevenSpices.Core.Items;

namespace SevenSpices.Core.Customers;

/// <summary>
/// 负责碗级食客评价与奖励派发。
/// 奖励内容（食材、道具）由调用方传入，不从 CustomerDefinition 推导。
/// 基础金币奖励由调用方传入，待后续游戏规则确定。
/// </summary>
public static class CustomerService
{
    /// <summary>
    /// 将食客实例指派为本碗的当前食客，并记录到本锅出现列表。
    /// </summary>
    public static void AssignCustomer(CustomerState state, CustomerInstance customer)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(customer);

        state.CurrentCustomer = customer;
        state.AppearedCustomers.Add(customer);
    }

    /// <summary>
    /// 评价当前食客并派发奖励，结算后清空 CurrentCustomer。
    /// 普通食客：派发 baseGoldReward 金币，不进行稀有满意度判断。
    /// 稀有食客：通过 SatisfactionEvaluator 判断满意度，满意则记录并派发奖励。
    /// 奖励食材写入 PlayerState.IngredientBasket，奖励道具写入 PlayerState.Items。
    /// </summary>
    public static bool EvaluateAndReward(
        CustomerState customerState,
        PotState pot,
        PlayerState player,
        int baseGoldReward = 0,
        IngredientInstance? rewardIngredient = null,
        ItemInstance? rewardItem = null)
    {
        ArgumentNullException.ThrowIfNull(customerState);
        ArgumentNullException.ThrowIfNull(pot);
        ArgumentNullException.ThrowIfNull(player);

        var customer = customerState.CurrentCustomer
            ?? throw new InvalidOperationException("No current customer to evaluate.");

        bool satisfied = false;

        if (!customer.Definition.IsRare)
        {
            player.Gold += baseGoldReward;
        }
        else
        {
            satisfied = SatisfactionEvaluator.IsSatisfied(customer.Definition, pot);

            if (satisfied)
            {
                customerState.SatisfiedRareCustomers.Add(customer);

                if (rewardIngredient != null)
                    player.IngredientBasket.Add(rewardIngredient);

                if (rewardItem != null)
                    player.Items.Add(rewardItem);
            }
        }

        customerState.CurrentCustomer = null;
        return satisfied;
    }
}
