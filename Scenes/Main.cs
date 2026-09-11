using Godot;
using SevenSpices.Core.Content;
using SevenSpices.Core.Effects;
using SevenSpices.Core.Game;
using SevenSpices.Core.Ingredients;
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
    private string[] _ingredientIds = null!;

    private Label _chapterLabel = null!;
    private Label _potLabel = null!;
    private Label _phaseLabel = null!;
    private Label _bowlLabel = null!;
    private Label _scoreLabel = null!;
    private Label _totalScoreLabel = null!;
    private Label _flavorLabel = null!;

    // 动态 tooltip 面板（顶层覆盖，不参与布局，不拦截鼠标）
    private PanelContainer _tooltipPanel = null!;
    private Label _tooltipLabel = null!;
    private bool _tooltipNeedsReposition;   // 等待下一帧取到真实 Size 后定位
    private Vector2 _tooltipAnchor;         // 期望放置的初始锚点（鼠标附近）

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
        _totalScoreLabel = MakeLabel("本锅累计基础分：0", center: true, minHeight: 28);
        vbox.AddChild(_totalScoreLabel);

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
        _ingredientIds = ids;
        _ingredientButtons = new Button[ids.Length];
        for (int i = 0; i < ids.Length; i++)
        {
            string capturedId = ids[i];
            var def = IngredientData.Registry.Get(capturedId);
            var btn = new Button();
            btn.Text = def.Name;
            btn.CustomMinimumSize = new Vector2(80, 36);
            btn.Pressed += () => OnIngredientPressed(capturedId);
            btn.MouseEntered += () => OnIngredientHover(capturedId);
            btn.MouseExited += HideTooltip;
            btnRow.AddChild(btn);
            _ingredientButtons[i] = btn;
        }

        // tooltip 面板挂到顶层覆盖 Control，脱离 vbox 布局流，
        // 设置 MouseFilter.Ignore 彻底不参与鼠标事件，防止闪烁。
        var tooltipOverlay = new Control();
        tooltipOverlay.AnchorRight = 1.0f;
        tooltipOverlay.AnchorBottom = 1.0f;
        tooltipOverlay.GrowHorizontal = Control.GrowDirection.Both;
        tooltipOverlay.GrowVertical = Control.GrowDirection.Both;
        tooltipOverlay.MouseFilter = Control.MouseFilterEnum.Ignore;
        AddChild(tooltipOverlay);

        _tooltipPanel = new PanelContainer();
        _tooltipPanel.Visible = false;
        _tooltipPanel.MouseFilter = Control.MouseFilterEnum.Ignore;
        tooltipOverlay.AddChild(_tooltipPanel);

        _tooltipLabel = new Label();
        _tooltipLabel.AutowrapMode = TextServer.AutowrapMode.Off;
        _tooltipLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
        _tooltipPanel.AddChild(_tooltipLabel);
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

    private void OnIngredientHover(string ingredientId)
    {
        var def = IngredientData.Registry.Get(ingredientId);
        var pot = _gameState.Pot;

        int? previewBaseScore = null;
        if (pot.Phase == PotPhase.InProgress && pot.CurrentBowlPhase == BowlPhase.IngredientResolve)
        {
            var inst = IngredientData.CreateInstance(ingredientId);
            var preview = _potController.PreviewIngredient(inst, _effectSystem);
            previewBaseScore = preview.PreviewBaseScore;
        }

        _tooltipLabel.Text = BuildIngredientTooltip(def, previewBaseScore);

        // 记录鼠标位置（tooltip 偏移显示在鼠标右下方），等下一帧 Size 确定后 clamp
        _tooltipAnchor = GetViewport().GetMousePosition() + new Vector2(14, 14);
        _tooltipPanel.Position = _tooltipAnchor;
        _tooltipPanel.Visible = true;
        _tooltipNeedsReposition = true;
    }

    private void HideTooltip()
    {
        _tooltipPanel.Visible = false;
        _tooltipNeedsReposition = false;
    }

    public override void _Process(double delta)
    {
        if (!_tooltipNeedsReposition || !_tooltipPanel.Visible)
            return;

        // PanelContainer 在下一帧完成最小尺寸计算后 Size 才准确
        var size = _tooltipPanel.Size;
        if (size == Vector2.Zero)
            return;     // 尺寸还未就绪，继续等

        _tooltipNeedsReposition = false;

        var viewport = GetViewport().GetVisibleRect().Size;
        float x = _tooltipAnchor.X;
        float y = _tooltipAnchor.Y;

        // 右侧超出 → 向左翻转到鼠标左侧
        if (x + size.X > viewport.X)
            x = _tooltipAnchor.X - 14 - 14 - size.X;  // 减去两侧偏移后贴鼠标左边

        // 下方超出 → 向上翻转到鼠标上方
        if (y + size.Y > viewport.Y)
            y = _tooltipAnchor.Y - 14 - 14 - size.Y;

        // 最终 clamp，确保不会超出左/上边界
        x = Mathf.Clamp(x, 0, Mathf.Max(0, viewport.X - size.X));
        y = Mathf.Clamp(y, 0, Mathf.Max(0, viewport.Y - size.Y));

        _tooltipPanel.Position = new Vector2(x, y);
    }

    private void OnIngredientPressed(string ingredientId)
    {
        var instance = IngredientData.CreateInstance(ingredientId);
        _potController.AddIngredient(instance, _effectSystem);

        // 食材投入后自动完成当前碗：从 IngredientResolve 推进到 End
        var pot = _gameState.Pot;
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
        _totalScoreLabel.Text = $"本锅累计基础分：{pot.TotalBaseScore}";
        _flavorLabel.Text = ToFlavorText(pot);

        bool canAct = pot.Phase == PotPhase.InProgress
                      && pot.CurrentBowlPhase == BowlPhase.IngredientResolve;
        foreach (var btn in _ingredientButtons)
            btn.Disabled = !canAct;
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

    // ── 食材 Tooltip ──────────────────────────────────────────────────────────

    private static string BuildIngredientTooltip(IngredientDefinition def, int? previewBaseScore = null)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"名称：{def.Name}");
        sb.AppendLine($"稀有度：{ToRarityText(def.Rarity)}");
        sb.AppendLine($"基础分：{def.BaseScore}");
        sb.AppendLine($"味道：{ToFlavorListText(def.Flavors)}");

        if (def.Effects.Count == 0)
        {
            sb.AppendLine("触发条件：无");
            sb.AppendLine("触发效果：无");
            sb.AppendLine("效果类型：基础");
        }
        else
        {
            foreach (var effect in def.Effects)
                sb.AppendLine(DescribeEffect(effect));
        }

        if (previewBaseScore.HasValue)
            sb.Append($"投入后预计基础分：{previewBaseScore.Value}");

        return sb.ToString().TrimEnd();
    }

    private static string ToRarityText(IngredientRarity rarity) => rarity switch
    {
        IngredientRarity.Common => "普通",
        IngredientRarity.Uncommon => "稀有",
        IngredientRarity.Rare => "罕见",
        IngredientRarity.Legendary => "传说",
        _ => rarity.ToString()
    };

    private static string ToFlavorListText(IReadOnlyDictionary<FlavorType, int> flavors)
    {
        if (flavors.Count == 0) return "无";
        var parts = new System.Collections.Generic.List<string>();
        foreach (var kv in flavors)
            parts.Add($"{ToFlavorName(kv.Key)} +{kv.Value}");
        return string.Join("  ", parts);
    }

    private static string ToFlavorName(FlavorType flavor) => flavor switch
    {
        FlavorType.Umami => "鲜",
        FlavorType.Sweet => "甜",
        FlavorType.Spicy => "辣",
        FlavorType.Sour => "酸",
        FlavorType.Bitter => "苦",
        _ => flavor.ToString()
    };

    private static string DescribeEffect(IEffect effect) => effect switch
    {
        ConditionalFlavorScoreEffect e =>
            $"触发条件：{ToFlavorName(e.Flavor)} ≥ {e.Threshold}\n触发效果：额外 +{e.Bonus} 分\n效果类型：条件奖励",
        ScaledFlavorScoreEffect e =>
            $"触发条件：每 {e.PerN} 点{ToFlavorName(e.Flavor)}\n触发效果：+{e.Bonus} 分\n效果类型：比例奖励",
        FinalScoreMultiplierEffect e =>
            $"触发条件：无\n触发效果：最终分数 ×{e.Multiplier:F1}\n效果类型：分数倍率",
        UniqueIngredientCountScoreEffect e =>
            $"触发条件：已有 {e.RequiredCount} 种不同食材\n触发效果：+{e.Bonus} 分\n效果类型：多样奖励",
        AddScoreEffect e =>
            $"触发条件：无\n触发效果：+{e.Amount} 分\n效果类型：固定加分",
        AddFlavorEffect e =>
            $"触发条件：无\n触发效果：{ToFlavorName(e.Flavor)} +{e.Amount}\n效果类型：增味",
        _ => $"触发条件：未知\n触发效果：{effect.EffectId}\n效果类型：未知"
    };
}
