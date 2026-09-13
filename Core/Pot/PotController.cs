using SevenSpices.Core.Bottom;
using SevenSpices.Core.Companions;
using SevenSpices.Core.Content;
using SevenSpices.Core.Effects;
using SevenSpices.Core.Flavors;
using SevenSpices.Core.Game;
using SevenSpices.Core.Ingredients;
using SevenSpices.Core.Items;
using SevenSpices.Core.Scoring;

namespace SevenSpices.Core.Pot;

/// <summary>
/// 负责一锅的基础流程：开锅、开碗、推进阶段、进入下一碗、结束锅。
/// 只控制流程，不保存业务状态（状态由 GameState / PotState 持有）。
/// </summary>
public class PotController
{
    private readonly GameState _gameState;

    /// <summary>伙伴系统（可选）：在固定时机调用伙伴扩展点。为 null 时行为与未持有伙伴完全一致。</summary>
    private readonly CompanionSystem? _companions;

    /// <summary>味道互动层：在加料瞬间按味道声明顺序结算味道动词。为 null 时使用默认系统。</summary>
    private readonly FlavorInteractionSystem _flavorInteraction;

    /// <summary>味道系统可调数值配置。为 null 时使用 <see cref="FlavorConfig.Default"/>。</summary>
    private readonly FlavorConfig _flavorConfig;

    public PotController(
        GameState gameState,
        CompanionSystem? companions = null,
        FlavorInteractionSystem? flavorInteraction = null,
        FlavorConfig? flavorConfig = null)
    {
        _gameState = gameState;
        _companions = companions;
        _flavorInteraction = flavorInteraction ?? FlavorInteractionSystem.Default;
        _flavorConfig = flavorConfig ?? FlavorConfig.Default;
    }

    public GameState GameState => _gameState;
    public PotState Pot => _gameState.Pot;

    /// <summary>
    /// 开始一锅：将锅标记为进行中，注入锅底。
    /// 普通锅碗数归 1；最终锅不逐碗结算，固定使用 <see cref="ScoreCalculator.FinalPotBowlNumber"/> 档位（×32）。
    /// 只能在 PotPhase.NotStarted 时调用。
    /// </summary>
    public void StartPot()
    {
        var pot = _gameState.Pot;
        if (pot.Phase != PotPhase.NotStarted)
            throw new InvalidOperationException($"Cannot start pot: pot phase is already {pot.Phase}.");

        if (_gameState.Run.IsFinalPot)
        {
            // 最终锅不逐碗结算：固定 ×32 档位，且不靠碗数上限结束（只能由 RunController.EndCooking 结束）
            pot.BowlNumber = ScoreCalculator.FinalPotBowlNumber;
            pot.BowlLimit = int.MaxValue;
        }
        else
        {
            pot.BowlNumber = 1;
        }

        pot.Phase = PotPhase.InProgress;
        _gameState.Bottom.ApplyToPot(pot);
    }

    /// <summary>
    /// 开始当前碗：重置碗内分数与锁定状态，进入 BowlPhase.Start。
    /// 最终锅不重置分数 —— 整锅作为一大碗一次性结算，基础分必须跨碗累积。
    /// 只能在 PotPhase.InProgress 时调用。
    /// </summary>
    public void StartBowl()
    {
        var pot = _gameState.Pot;
        if (pot.Phase != PotPhase.InProgress)
            throw new InvalidOperationException($"Cannot start bowl: pot phase is {pot.Phase}.");

        if (!_gameState.Run.IsFinalPot)
        {
            pot.BaseScore = 0;
            pot.FinalScore = 0;
            pot.IsScoreLocked = false;
        }

        pot.CurrentBowlPhase = BowlPhase.Start;
    }

    /// <summary>
    /// 推进当前碗至下一阶段。
    /// 当碗到达 BowlPhase.End 且已达碗数上限时，锅自动进入 PotPhase.Ended。
    /// </summary>
    public void AdvanceBowlPhase()
    {
        var pot = _gameState.Pot;
        if (pot.Phase != PotPhase.InProgress)
            throw new InvalidOperationException($"Cannot advance bowl phase: pot phase is {pot.Phase}.");
        if (pot.CurrentBowlPhase == BowlPhase.End)
            throw new InvalidOperationException("Current bowl is already at End. Call StartNextBowl(), or check if the pot has ended.");

        pot.CurrentBowlPhase = (BowlPhase)((int)pot.CurrentBowlPhase + 1);

        if (pot.CurrentBowlPhase == BowlPhase.End && pot.BowlNumber >= pot.BowlLimit)
            pot.Phase = PotPhase.Ended;
    }

    /// <summary>
    /// 推进到下一碗：碗数 +1，重置碗状态，进入 BowlPhase.Start。
    /// 要求当前碗已到达 BowlPhase.End 且锅仍处于 InProgress（即未到上限）。
    /// 最终锅禁止调用：最终锅不逐碗结算，只能通过 RunController.EndCooking() 整锅一次性结算。
    /// </summary>
    public void StartNextBowl()
    {
        var pot = _gameState.Pot;
        if (pot.Phase != PotPhase.InProgress)
            throw new InvalidOperationException($"Cannot start next bowl: pot phase is {pot.Phase}.");
        if (_gameState.Run.IsFinalPot)
            throw new InvalidOperationException(
                "Cannot start next bowl: Final Pot is settled as a single bowl by RunController.EndCooking().");
        if (pot.CurrentBowlPhase != BowlPhase.End)
            throw new InvalidOperationException($"Cannot start next bowl: current bowl phase is {pot.CurrentBowlPhase}.");

        pot.BowlNumber++;
        StartBowl();
    }

    /// <summary>
    /// 强制结束当前锅（供特殊游戏机制使用）。
    /// 仅在 PotPhase.InProgress 时有效。
    /// </summary>
    public void EndPot()
    {
        var pot = _gameState.Pot;
        if (pot.Phase != PotPhase.InProgress)
            throw new InvalidOperationException($"Cannot end pot: pot phase is {pot.Phase}.");
        pot.Phase = PotPhase.Ended;
    }

    /// <summary>
    /// 消耗一个道具。合法时机为「加入食材之前」：ItemPhase 或 IngredientSelection
    /// （设计文档 §十五：必须在加入食材之前使用）。
    /// 从 PlayerState.Items 中移除并返回对应 ItemInstance，不执行任何效果。
    /// </summary>
    public ItemInstance UseItem(string instanceId)
    {
        var pot = _gameState.Pot;
        if (!IsItemUsablePhase(pot.CurrentBowlPhase))
            throw new InvalidOperationException(
                $"Cannot use item: current bowl phase is {pot.CurrentBowlPhase}, expected ItemPhase or IngredientSelection.");

        if (string.IsNullOrWhiteSpace(instanceId))
            throw new ArgumentException("instanceId cannot be empty.", nameof(instanceId));

        var items = _gameState.Player.Items;
        var item = items.FirstOrDefault(i => i.InstanceId == instanceId)
            ?? throw new ArgumentException($"Item instance '{instanceId}' not found in PlayerState.Items.", nameof(instanceId));

        items.Remove(item);
        return item;
    }

    /// <summary>
    /// 在 IngredientResolve 阶段将食材加入锅：先应用 Definition.BaseScore 和 Definition.Flavors，
    /// 再通过 EffectSystem 触发效果链。效果触发时能读到包含本食材基础数据的最新状态。
    /// 本锅累计基础分按「(BaseScore + FlavorScore)」的加入前后差值记账，包含味道分变化。
    /// </summary>
    public void AddIngredient(IngredientInstance ingredient, EffectSystem effectSystem)
    {
        ArgumentNullException.ThrowIfNull(ingredient);
        ArgumentNullException.ThrowIfNull(effectSystem);

        var pot = _gameState.Pot;
        if (pot.CurrentBowlPhase != BowlPhase.IngredientResolve)
            throw new InvalidOperationException(
                $"Cannot add ingredient: current bowl phase is {pot.CurrentBowlPhase}, expected IngredientResolve.");

        double totalBefore = pot.BaseScore + pot.FlavorScore;
        pot.Ingredients.Add(ingredient);
        ApplyIngredientTo(ingredient, pot, _gameState, effectSystem);
        pot.TotalBaseScore += (int)Math.Floor(pot.BaseScore + pot.FlavorScore - totalBefore);
    }

    /// <summary>
    /// 预测将某食材加入当前碗后的结果，不修改任何真实状态。
    /// 对当前 PotState 做快照拷贝，在拷贝上运行与 AddIngredient 相同的计算逻辑，返回预测结果。
    /// 可在 IngredientSelection（悬停候选时）或 IngredientResolve 阶段调用。
    /// </summary>
    public IngredientPreview PreviewIngredient(IngredientInstance ingredient, EffectSystem effectSystem)
    {
        ArgumentNullException.ThrowIfNull(ingredient);
        ArgumentNullException.ThrowIfNull(effectSystem);

        var pot = _gameState.Pot;
        if (pot.CurrentBowlPhase != BowlPhase.IngredientResolve
            && pot.CurrentBowlPhase != BowlPhase.IngredientSelection)
            throw new InvalidOperationException(
                $"Cannot preview ingredient: current bowl phase is {pot.CurrentBowlPhase}, expected IngredientSelection or IngredientResolve.");

        var snapshot = PotStateSnapshot.From(pot);
        snapshot.Ingredients.Add(ingredient);
        ApplyIngredientTo(ingredient, snapshot, _gameState, effectSystem);

        return new IngredientPreview(snapshot.BaseScore, ScoreCalculator.ComputeFinalScore(snapshot));
    }

    /// <summary>
    /// 将食材的基础分、味道和效果链应用到目标 PotState（真实或快照）。
    /// AddIngredient 和 PreviewIngredient 共享此方法，确保计算逻辑完全一致（含伙伴 E1 修正）。
    /// 调用方负责在调用前先将 ingredient 加入 pot.Ingredients（以便 UniqueCount 等效果能感知）。
    /// </summary>
    private void ApplyIngredientTo(
        IngredientInstance ingredient, PotState pot, GameState gameState, EffectSystem effectSystem)
    {
        // 伙伴扩展点 E1：只改基础分，不改其它状态；无伙伴时等价于原值。
        int baseScore = _companions?.ModifyIngredientBaseScore(ingredient, ingredient.Definition.BaseScore)
            ?? ingredient.Definition.BaseScore;

        pot.BaseScore += baseScore;
        foreach (var (flavor, amount) in ingredient.Definition.Flavors)
            pot.AddFlavor(flavor, amount);

        // 味道互动层：在基础味道应用后、食材特殊效果前结算（设计文档 §七 / §11.1）。
        _flavorInteraction.Resolve(pot, ingredient, _flavorConfig);

        var context = new EffectContext(pot.BowlNumber, gameState, pot, ingredient);
        effectSystem.TriggerAll(ingredient.Definition.Effects, ingredient.InstanceId, context);
    }

    /// <summary>
    /// 为已消耗的道具触发其效果链。合法时机与 <see cref="UseItem"/> 一致：
    /// ItemPhase 或 IngredientSelection（加入食材之前，设计文档 §十五）。
    /// 通常在 UseItem() 之后调用。
    /// </summary>
    public void ApplyItemEffect(ItemInstance item, EffectSystem effectSystem)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(effectSystem);

        var pot = _gameState.Pot;
        if (!IsItemUsablePhase(pot.CurrentBowlPhase))
            throw new InvalidOperationException(
                $"Cannot apply item effect: current bowl phase is {pot.CurrentBowlPhase}, expected ItemPhase or IngredientSelection.");

        var context = new EffectContext(pot.BowlNumber, _gameState, pot, currentIngredient: null);
        effectSystem.TriggerAll(item.Definition.Effects, item.InstanceId, context);
    }

    /// <summary>
    /// 道具使用时机判定：加入食材之前，即 ItemPhase 或 IngredientSelection。
    /// </summary>
    private static bool IsItemUsablePhase(BowlPhase phase) =>
        phase == BowlPhase.ItemPhase || phase == BowlPhase.IngredientSelection;

    /// <summary>
    /// 在 ScoreCalculation 阶段计算并锁定本碗分数。
    /// 调用 ScoreCalculator.CalculateAndLock，将 BaseScore × 倍率写入 FinalScore 并锁定。
    /// </summary>
    public void CalculateScore()
    {
        var pot = _gameState.Pot;
        if (pot.CurrentBowlPhase != BowlPhase.ScoreCalculation)
            throw new InvalidOperationException(
                $"Cannot calculate score: current bowl phase is {pot.CurrentBowlPhase}, expected ScoreCalculation.");

        ScoreCalculator.CalculateAndLock(pot);
    }

    /// <summary>
    /// 锅结束后提炼锅底：从当前 PotState 的 Flavor 提取 30% 写入 BottomState。
    /// 只能在 PotPhase.Ended 时调用，是普通锅与最终锅共用的锅级收尾入口。
    /// </summary>
    public void ClosePot()
    {
        var pot = _gameState.Pot;
        if (pot.Phase != PotPhase.Ended)
            throw new InvalidOperationException(
                $"Cannot close pot: pot phase is {pot.Phase}, expected Ended.");

        BottomExtractor.Extract(pot, _gameState.Bottom);

        // 伙伴扩展点 E2：提炼后允许伙伴修正锅底（只读 pot、只写 bottom）；无伙伴时不执行。
        _companions?.ApplyBottomSettlementHooks(pot, _gameState.Bottom);
    }
}
