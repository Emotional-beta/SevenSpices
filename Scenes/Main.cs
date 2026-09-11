using Godot;
using SevenSpices.Core.Game;
using SevenSpices.Core.Run;

namespace SevenSpices;

public partial class Main : Node
{
    private GameState _gameState = null!;
    private RunController _runController = null!;

    public override void _Ready()
    {
        _gameState = new GameState();
        _runController = new RunController(_gameState);

        _runController.StartRun();
        _runController.StartCurrentPot();

        GD.Print("七荤八素启动");
        GD.Print($"Chapter: {_gameState.Run.Chapter}");
        GD.Print($"Pot: {_gameState.Run.PotIndex}");
        GD.Print($"Final Pot: {_gameState.Run.IsFinalPot}");
        GD.Print($"Phase: {_gameState.Pot.Phase}");

        RefreshUI();
    }

    private void RefreshUI()
    {
        var run = _gameState.Run;
        var pot = _gameState.Pot;

        GetNode<Label>("%ChapterLabel").Text = $"第 {run.Chapter} 章";
        GetNode<Label>("%PotLabel").Text = $"第 {run.PotIndex} 锅";
        GetNode<Label>("%PhaseLabel").Text = $"阶段：{ToBowlPhaseText(pot.CurrentBowlPhase)}";
        GetNode<Label>("%BowlLabel").Text = $"碗数：{pot.BowlNumber} / {ToBowlLimitText(pot.BowlLimit)}";
        GetNode<Label>("%ScoreLabel").Text = $"基础分：{pot.BaseScore}";
        GetNode<Label>("%FlavorLabel").Text = ToFlavorText(pot);
    }

    private static string ToBowlPhaseText(BowlPhase phase) => phase switch
    {
        BowlPhase.Start => "开始",
        BowlPhase.Customer => "食客出现",
        BowlPhase.ItemPhase => "使用道具",
        BowlPhase.IngredientSelection => "选择食材",
        BowlPhase.IngredientResolve => "投入食材",
        BowlPhase.ScoreCalculation => "计算分数",
        BowlPhase.ScoreLocked => "分数锁定",
        BowlPhase.Serving => "呈上料理",
        BowlPhase.Reward => "获得奖励",
        BowlPhase.End => "本碗结束",
        _ => phase.ToString()
    };

    private static string ToBowlLimitText(int limit) =>
        limit == int.MaxValue ? "∞" : limit.ToString();

    private static string ToFlavorText(PotState pot)
    {
        (string label, FlavorType type)[] flavors =
        [
            ("鲜", FlavorType.Umami),
            ("甜", FlavorType.Sweet),
            ("辣", FlavorType.Spicy),
            ("酸", FlavorType.Sour),
            ("苦", FlavorType.Bitter),
        ];
        var parts = System.Array.ConvertAll(flavors, f => $"{f.label} {pot.GetFlavor(f.type)}");
        return string.Join("   ", parts);
    }
}
