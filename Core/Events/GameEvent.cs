using SevenSpices.Core.Companions;
using SevenSpices.Core.Customers;
using SevenSpices.Core.Ingredients;
using SevenSpices.Core.Items;
using SevenSpices.Core.Shop;

namespace SevenSpices.Core.Events;

/// <summary>
/// 所有游戏事件的抽象基类。
/// 具体事件一律用 sealed 类，并只携带只读数据（构造后不可变）。
/// 纯 C#，不依赖 Godot。
/// </summary>
public abstract class GameEvent
{
}

/// <summary>一锅开始（已注入锅底、进入 InProgress）。</summary>
public sealed class PotStartedEvent : GameEvent
{
    public int Chapter { get; }
    public int PotIndex { get; }
    public bool IsFinalPot { get; }

    public PotStartedEvent(int chapter, int potIndex, bool isFinalPot)
    {
        Chapter = chapter;
        PotIndex = potIndex;
        IsFinalPot = isFinalPot;
    }
}

/// <summary>一碗开始（碗号已确定）。</summary>
public sealed class BowlStartedEvent : GameEvent
{
    public int BowlNumber { get; }

    public BowlStartedEvent(int bowlNumber)
    {
        BowlNumber = bowlNumber;
    }
}

/// <summary>本轮抽取候选完成。携带候选的快照副本，避免被后续回池/清空影响。</summary>
public sealed class IngredientDrawnEvent : GameEvent
{
    public IReadOnlyList<IngredientInstance> Candidates { get; }

    public IngredientDrawnEvent(IReadOnlyList<IngredientInstance> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        Candidates = candidates.ToArray();
    }
}

/// <summary>一个食材已投入锅中（基础分、味道、效果链均已结算）。</summary>
public sealed class IngredientAddedEvent : GameEvent
{
    public IngredientInstance Ingredient { get; }

    public IngredientAddedEvent(IngredientInstance ingredient)
    {
        Ingredient = ingredient ?? throw new ArgumentNullException(nameof(ingredient));
    }
}

/// <summary>一个道具已被消耗并完成效果结算。</summary>
public sealed class ItemUsedEvent : GameEvent
{
    public ItemInstance Item { get; }

    public ItemUsedEvent(ItemInstance item)
    {
        Item = item ?? throw new ArgumentNullException(nameof(item));
    }
}

/// <summary>一个效果被执行（触发源与效果 ID）。</summary>
public sealed class EffectTriggeredEvent : GameEvent
{
    public string EffectId { get; }
    public string SourceId { get; }

    public EffectTriggeredEvent(string effectId, string sourceId)
    {
        EffectId = effectId ?? throw new ArgumentNullException(nameof(effectId));
        SourceId = sourceId ?? throw new ArgumentNullException(nameof(sourceId));
    }
}

/// <summary>一碗分数计算完成（倍率已应用、写入 FinalScore 之前/同时）。</summary>
public sealed class ScoreCalculatedEvent : GameEvent
{
    /// <summary>
    /// 含味道分的基础分（<c>floor(BaseScore + FlavorScore)</c>），即乘倍率前的实际计分基数。
    /// 语义与设计文档一致：基础分包含食材基础分、其它效果分与味道分。
    /// </summary>
    public int BaseScore { get; }

    /// <summary>倍率应用后的最终分数。</summary>
    public int FinalScore { get; }

    /// <summary>本碗应用的分值倍率。</summary>
    public int Multiplier { get; }

    public ScoreCalculatedEvent(int baseScore, int finalScore, int multiplier)
    {
        BaseScore = baseScore;
        FinalScore = finalScore;
        Multiplier = multiplier;
    }
}

/// <summary>一碗或最终锅分数已锁定。</summary>
public sealed class ScoreLockedEvent : GameEvent
{
    public int FinalScore { get; }

    public ScoreLockedEvent(int finalScore)
    {
        FinalScore = finalScore;
    }
}

/// <summary>一位食客已喝粥并完成奖励派发。</summary>
public sealed class CustomerServedEvent : GameEvent
{
    public CustomerInstance Customer { get; }
    public int GoldAwarded { get; }

    public CustomerServedEvent(CustomerInstance customer, int goldAwarded)
    {
        Customer = customer ?? throw new ArgumentNullException(nameof(customer));
        GoldAwarded = goldAwarded;
    }
}

/// <summary>一位稀有食客判定为满意（在 <see cref="CustomerServedEvent"/> 之后发布）。</summary>
public sealed class CustomerSatisfiedEvent : GameEvent
{
    public CustomerInstance Customer { get; }

    public CustomerSatisfiedEvent(CustomerInstance customer)
    {
        Customer = customer ?? throw new ArgumentNullException(nameof(customer));
    }
}

/// <summary>一锅结束（含最终锅）。</summary>
public sealed class PotEndedEvent : GameEvent
{
    public int Chapter { get; }
    public int PotIndex { get; }
    public bool IsFinalPot { get; }

    public PotEndedEvent(int chapter, int potIndex, bool isFinalPot)
    {
        Chapter = chapter;
        PotIndex = potIndex;
        IsFinalPot = isFinalPot;
    }
}

/// <summary>普通锅结束后的 X 选 1 奖励候选已生成。</summary>
public sealed class RewardOfferedEvent : GameEvent
{
    public IReadOnlyList<IngredientInstance> Candidates { get; }

    public RewardOfferedEvent(IReadOnlyList<IngredientInstance> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        Candidates = candidates.ToArray();
    }
}

/// <summary>锅结束奖励已选定。</summary>
public sealed class RewardChosenEvent : GameEvent
{
    public IngredientInstance Ingredient { get; }

    public RewardChosenEvent(IngredientInstance ingredient)
    {
        Ingredient = ingredient ?? throw new ArgumentNullException(nameof(ingredient));
    }
}

/// <summary>整局完成（最终锅结算完毕）。</summary>
public sealed class RunCompletedEvent : GameEvent
{
}

/// <summary>玩家获得一位伙伴（已加入 PlayerState.Companions）。</summary>
public sealed class CompanionAddedEvent : GameEvent
{
    public CompanionInstance Companion { get; }

    public CompanionAddedEvent(CompanionInstance companion)
    {
        Companion = companion ?? throw new ArgumentNullException(nameof(companion));
    }
}

/// <summary>
/// 普通锅结束后的商店报价已生成（设计文档 §18）。
/// <see cref="Offers"/> 是报价列表的复制（独立数组，之后增删报价不影响本事件）；
/// 但其中的 <see cref="ShopOffer"/> 元素是共享的可变引用，购买后其
/// <see cref="ShopOffer.IsPurchased"/> 会反映到本事件已携带的报价上。
/// </summary>
public sealed class ShopOfferedEvent : GameEvent
{
    public IReadOnlyList<ShopOffer> Offers { get; }

    public ShopOfferedEvent(IReadOnlyList<ShopOffer> offers)
    {
        ArgumentNullException.ThrowIfNull(offers);
        Offers = offers.ToArray();
    }
}

/// <summary>玩家从商店购买了一件报价（金币已扣除、物品已入账）。</summary>
public sealed class ShopPurchasedEvent : GameEvent
{
    public ShopOffer Offer { get; }

    public ShopPurchasedEvent(ShopOffer offer)
    {
        Offer = offer ?? throw new ArgumentNullException(nameof(offer));
    }
}

/// <summary>玩家跳过商店（或不再购买）：报价已清空、商店环节已结算。</summary>
public sealed class ShopSkippedEvent : GameEvent
{
    /// <summary>跳过时被清空的报价数量。</summary>
    public int OfferCount { get; }

    public ShopSkippedEvent(int offerCount)
    {
        OfferCount = offerCount;
    }
}

/// <summary>玩家跳过伙伴选择（或不再选择）：候选已清空、伙伴环节已处理，不获得任何伙伴。</summary>
public sealed class CompanionChoiceSkippedEvent : GameEvent
{
    /// <summary>跳过时被清空的候选数量。</summary>
    public int CandidateCount { get; }

    public CompanionChoiceSkippedEvent(int candidateCount)
    {
        CandidateCount = candidateCount;
    }
}

