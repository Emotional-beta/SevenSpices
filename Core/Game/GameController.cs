using SevenSpices.Core.Content;
using SevenSpices.Core.Effects;
using SevenSpices.Core.Ingredients;
using SevenSpices.Core.Pot;
using SevenSpices.Core.Run;

namespace SevenSpices.Core.Game;

/// <summary>
/// 一局游戏对表现层的统一入口（Game Layer）。
/// 持有 GameState + RunController + EffectSystem，封装游戏流程阶段的推进与玩家动作。
/// 纯 C#，不依赖 Godot / 场景节点。
/// </summary>
public class GameController
{
    private readonly GameState _state;
    private readonly RunController _runController;
    private readonly EffectSystem _effectSystem;
    private PotController? _potController;

    /// <param name="state">可注入已有 GameState（测试 / 存档恢复用）；为空则新建。</param>
    public GameController(GameState? state = null)
    {
        _state = state ?? new GameState();
        _runController = new RunController(_state);
        _effectSystem = new EffectSystem();
    }

    /// <summary>游戏运行状态根。</summary>
    public GameState State => _state;

    public RunState Run => _state.Run;
    public PotState Pot => _state.Pot;
    public PlayerState Player => _state.Player;

    /// <summary>当前是否处于最终锅。</summary>
    public bool IsFinalPot => _state.Run.IsFinalPot;

    /// <summary>当前是否允许投入食材（锅进行中且处于 IngredientResolve 阶段）。</summary>
    public bool CanAddIngredient =>
        _state.Pot.Phase == PotPhase.InProgress
        && _state.Pot.CurrentBowlPhase == BowlPhase.IngredientResolve;

    /// <summary>
    /// 开始一局新游戏：启动 Run、启动第 1 锅、开始第 1 碗，并推进到 IngredientResolve。
    /// 等价于表现层原先在 _Ready 中的启动流程。
    /// </summary>
    public void StartNewGame()
    {
        _runController.StartRun();
        _potController = _runController.StartCurrentPot();
        _potController.StartBowl();

        // Start → Customer → ItemPhase → IngredientSelection → IngredientResolve
        for (int i = 0; i < 4; i++)
            _potController.AdvanceBowlPhase();
    }

    /// <summary>
    /// 投入一个食材：创建实例、交给 PotController 结算基础分与效果。
    /// 普通锅会接着自动完成当前碗并进入下一碗（原型行为，见下方注释）。
    /// 最终锅只累积食材，不逐碗推进。
    /// </summary>
    /// <returns>实际加入锅的食材实例。</returns>
    public IngredientInstance AddIngredient(string definitionId)
    {
        var potController = EnsurePotController();

        var instance = IngredientData.CreateInstance(definitionId);
        potController.AddIngredient(instance, _effectSystem);

        // 临时原型行为：投入食材后整碗自动推进到 End，并直接开始下一碗。
        // A3 会把它替换为正式的逐阶段玩家流程，此处仅为保持当前可观察行为不变。
        if (!_state.Run.IsFinalPot)
        {
            while (_state.Pot.CurrentBowlPhase != BowlPhase.End
                   && _state.Pot.Phase == PotPhase.InProgress)
            {
                if (_state.Pot.CurrentBowlPhase == BowlPhase.ScoreCalculation)
                    potController.CalculateScore();
                potController.AdvanceBowlPhase();
            }

            if (_state.Pot.Phase == PotPhase.InProgress)
            {
                potController.StartNextBowl();
                for (int i = 0; i < 4; i++)
                    potController.AdvanceBowlPhase();
            }
        }

        return instance;
    }

    /// <summary>
    /// 预测将某食材投入当前碗后的本碗基础分，不修改任何真实状态。
    /// 只能在 IngredientResolve 阶段调用（调用前用 CanAddIngredient 判断）。
    /// </summary>
    public IngredientPreview PreviewIngredient(string definitionId)
    {
        var potController = EnsurePotController();

        var instance = IngredientData.CreateInstance(definitionId);
        return potController.PreviewIngredient(instance, _effectSystem);
    }

    /// <summary>
    /// 结束煮粥：结算最终锅。只能在最终锅且锅进行中调用。
    /// 食客与奖励规则尚未接入内容层，暂用正式普通食客数据占位（保持现状）。
    /// </summary>
    public void EndCooking()
    {
        _runController.EndCooking(CustomerData.CreateNormalInstance());
    }

    /// <summary>
    /// 获取当前锅的 PotController。
    /// 正常情况下由 StartNewGame 创建；若调用方注入了已进行中的 GameState
    /// （测试 / 存档恢复），则基于同一状态惰性创建，不再重复 StartPot。
    /// </summary>
    private PotController EnsurePotController()
        => _potController ??= new PotController(_state);
}
