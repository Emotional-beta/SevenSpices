using Godot;
using SevenSpices.Core.Content;
using SevenSpices.Core.Effects;
using SevenSpices.Core.Game;
using SevenSpices.Core.Pot;
using SevenSpices.Core.Run;

namespace SevenSpices;

public partial class Main : Node
{
    private GameState _gameState = null!;
    private RunController _runController = null!;
    private PotController _potController = null!;
    private EffectSystem _effectSystem = null!;
    private Button[] _ingredientButtons = null!;
    private Button _completeBowlButton = null!;

    private Label _chapterLabel = null!;
    private Label _potLabel = null!;
    private Label _phaseLabel = null!;
    private Label _bowlLabel = null!;
    private Label _scoreLabel = null!;
    private Label _flavorLabel = null!;

    public override void _Ready()
    {
        _gameState = new GameState();
        _runController = new RunController(_gameState);
        _effectSystem = new EffectSystem();

        _runController.StartRun();
        _potController = _runController.StartCurrentPot();

        // 正式开始第 1 碗，并推进到 IngredientResolve
        _potController.StartBowl();
        for (int i = 0; i < 4; i++)
            _potController.AdvanceBowlPhase();

        GD.Print("七荤八素启动");
        GD.Print($"Chapter: {_gameState.Run.Chapter}");
        GD.Print($"Pot: {_gameState.Run.PotIndex}");
        GD.Print($"Final Pot: {_gameState.Run.IsFinalPot}");
        GD.Print($"Phase: {_gameState.Pot.Phase}");

        BuildUI();
        RefreshUI();
    }

    private void BuildUI()
    {
        var uiRoot = new Control();
        uiRoot.AnchorRight = 1.0f;
        uiRoot.AnchorBottom = 1.0f;
        uiRoot.GrowHorizontal = Control.GrowDirection.Both;
        uiRoot.GrowVertical = Control.GrowDirection.Both;
        AddChild(uiRoot);

        var margin = new MarginContainer();
        margin.AnchorRight = 1.0f;
        margin.AnchorBottom = 1.0f;
        margin.GrowHorizontal = Control.GrowDirection.Both;
        margin.GrowVertical = Control.GrowDirection.Both;
        margin.AddThemeConstantOverride("margin_left", 20);
        margin.AddThemeConstantOverride("margin_top", 20);
        margin.AddThemeConstantOverride("margin_right", 20);
        margin.AddThemeConstantOverride("margin_bottom", 20);
        uiRoot.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 20);
        margin.AddChild(vbox);

        vbox.AddChild(MakeLabel("七荤八素", center: true, minHeight: 40));

        var runRow = new HBoxContainer();
        runRow.Alignment = BoxContainer.AlignmentMode.Center;
        runRow.AddThemeConstantOverride("separation", 40);
        vbox.AddChild(runRow);
        _chapterLabel = MakeLabel("第 1 章");
        runRow.AddChild(_chapterLabel);
        _potLabel = MakeLabel("第 1 锅");
        runRow.AddChild(_potLabel);

        _phaseLabel = MakeLabel("阶段：--", center: true, minHeight: 28);
        vbox.AddChild(_phaseLabel);
        _bowlLabel = MakeLabel("碗数：-- / --", center: true, minHeight: 28);
        vbox.AddChild(_bowlLabel);
        _scoreLabel = MakeLabel("基础分：0", center: true, minHeight: 28);
        vbox.AddChild(_scoreLabel);

        var sep1 = new HSeparator();
        sep1.CustomMinimumSize = new Vector2(0, 10);
        vbox.AddChild(sep1);

        vbox.AddChild(MakeLabel("味道", center: true, minHeight: 28));
        _flavorLabel = MakeLabel("--", center: true, minHeight: 28);
        vbox.AddChild(_flavorLabel);

        var sep2 = new HSeparator();
        sep2.CustomMinimumSize = new Vector2(0, 10);
        vbox.AddChild(sep2);

        vbox.AddChild(MakeLabel("投入食材", center: true, minHeight: 28));

        var btnRow = new HBoxContainer();
        btnRow.Alignment = BoxContainer.AlignmentMode.Center;
        btnRow.AddThemeConstantOverride("separation", 15);
        vbox.AddChild(btnRow);

        string[] ids = { "rice", "sugar", "pepper", "red_date" };
        _ingredientButtons = new Button[ids.Length];
        for (int i = 0; i < ids.Length; i++)
        {
            string capturedId = ids[i];
            var def = IngredientData.Registry.Get(capturedId);
            var btn = new Button();
            btn.Text = def.Name;
            btn.CustomMinimumSize = new Vector2(80, 36);
            btn.Pressed += () => OnIngredientPressed(capturedId);
            btnRow.AddChild(btn);
            _ingredientButtons[i] = btn;
        }

        // 完成本碗按钮
        var sep3 = new HSeparator();
        sep3.CustomMinimumSize = new Vector2(0, 10);
        vbox.AddChild(sep3);

        _completeBowlButton = new Button();
        _completeBowlButton.Text = "完成本碗";
        _completeBowlButton.CustomMinimumSize = new Vector2(160, 40);
        _completeBowlButton.Pressed += OnCompleteBowlPressed;
        var completeBowlRow = new HBoxContainer();
        completeBowlRow.Alignment = BoxContainer.AlignmentMode.Center;
        completeBowlRow.AddChild(_completeBowlButton);
        vbox.AddChild(completeBowlRow);
    }

    private static Label MakeLabel(string text, bool center = false, int minHeight = 0)
    {
        var lbl = new Label();
        lbl.Text = text;
        if (center)
        {
            lbl.HorizontalAlignment = HorizontalAlignment.Center;
            lbl.VerticalAlignment = VerticalAlignment.Center;
        }
        if (minHeight > 0)
            lbl.CustomMinimumSize = new Vector2(0, minHeight);
        return lbl;
    }

    private void OnIngredientPressed(string ingredientId)
    {
        var instance = IngredientData.CreateInstance(ingredientId);
        _potController.AddIngredient(instance, _effectSystem);
        RefreshUI();
    }

    private void OnCompleteBowlPressed()
    {
        var pot = _gameState.Pot;

        // 从 IngredientResolve 推进到 End（共 5 步）
        while (pot.CurrentBowlPhase != BowlPhase.End && pot.Phase == PotPhase.InProgress)
            _potController.AdvanceBowlPhase();

        // 未达上限则开始下一碗，并推进到 IngredientResolve
        if (pot.Phase == PotPhase.InProgress)
        {
            _potController.StartNextBowl();
            for (int i = 0; i < 4; i++)
                _potController.AdvanceBowlPhase();
        }

        RefreshUI();
    }

    private void RefreshUI()
    {
        var run = _gameState.Run;
        var pot = _gameState.Pot;

        _chapterLabel.Text = $"第 {run.Chapter} 章";
        _potLabel.Text = $"第 {run.PotIndex} 锅";
        _phaseLabel.Text = $"阶段：{ToBowlPhaseText(pot.CurrentBowlPhase)}";
        _bowlLabel.Text = $"碗数：{pot.BowlNumber} / {ToBowlLimitText(pot.BowlLimit)}";
        _scoreLabel.Text = $"基础分：{pot.BaseScore}";
        _flavorLabel.Text = ToFlavorText(pot);

        bool canAct = pot.Phase == PotPhase.InProgress
                      && pot.CurrentBowlPhase == BowlPhase.IngredientResolve;
        foreach (var btn in _ingredientButtons)
            btn.Disabled = !canAct;
        _completeBowlButton.Disabled = !canAct;
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
