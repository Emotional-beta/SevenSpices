using Godot;
using SevenSpices.Core.Game;
using SevenSpices.Core.Run;

namespace SevenSpices;

public partial class Main : Node
{
    public override void _Ready()
    {
        var gameState = new GameState();
        var runController = new RunController(gameState);

        runController.StartRun();
        runController.StartCurrentPot();

        var run = gameState.Run;
        var pot = gameState.Pot;

        GD.Print("七荤八素启动");
        GD.Print($"Chapter: {run.Chapter}");
        GD.Print($"Pot: {run.PotIndex}");
        GD.Print($"Final Pot: {run.IsFinalPot}");
        GD.Print($"Phase: {pot.Phase}");
    }
}
