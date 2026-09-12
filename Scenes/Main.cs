using Godot;
using SevenSpices.Core.Effects;
using SevenSpices.Core.Game;
using SevenSpices.Core.Ingredients;
using SevenSpices.Core.Scoring;

namespace SevenSpices;

public partial class Main : Node
{
    private GameController _controller = null!;
    private HBoxContainer _candidateRow = null!;
    private Button _skipBowlButton = null!;
    private Label _poolCountLabel = null!;

    private Label _chapterLabel = null!;
    private Label _potLabel = null!;
    private Label _phaseLabel = null!;
    private Label _bowlLabel = null!;
    private Label _scoreLabel = null!;
    private Label _multiplierLabel = null!;
    private Label _finalScoreLabel = null!;
    private Label _totalScoreLabel = null!;
    private Label _customerLabel = null!;
    private Label _goldLabel = null!;
    private Label _flavorLabel = null!;
    private Label _bottomLabel = null!;
    private Label _runCompleteLabel = null!;
    private Button _nextPotButton = null!;
    private Button _endCookingButton = null!;

    // 动态 tooltip 面板（顶层覆盖，不参与布局，不拦截鼠标）
    private PanelContainer _tooltipPanel = null!;
    private Label _tooltipLabel = null!;
    private bool _tooltipNeedsReposition;   // 等待下一帧取到真实 Size 后定位
    private Vector2 _tooltipAnchor;         // 期望放置的初始锚点（鼠标附近）

    public override void _Ready()
    {
        _controller = new GameController();
        _controller.StartNewGame();

        GD.Print("七荤八素启动");
        GD.Print($"Chapter: {_controller.State.Run.Chapter}");
        GD.Print($"Pot: {_controller.State.Run.PotIndex}");
        GD.Print($"Final Pot: {_controller.State.Run.IsFinalPot}");
        GD.Print($"Phase: {_controller.State.Pot.Phase}");

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
        _multiplierLabel = MakeLabel("倍率：×1", center: true, minHeight: 28);
        vbox.AddChild(_multiplierLabel);
        _finalScoreLabel = MakeLabel("最终分数：0", center: true, minHeight: 28);
        vbox.AddChild(_finalScoreLabel);
        _totalScoreLabel = MakeLabel("本锅累计基础分：0", center: true, minHeight: 28);
        vbox.AddChild(_totalScoreLabel);
        _customerLabel = MakeLabel("当前食客：--", center: true, minHeight: 28);
        vbox.AddChild(_customerLabel);
        _goldLabel = MakeLabel("金币：0", center: true, minHeight: 28);
        vbox.AddChild(_goldLabel);

        var sep1 = new HSeparator();
        sep1.CustomMinimumSize = new Vector2(0, 10);
        vbox.AddChild(sep1);

        vbox.AddChild(MakeLabel("味道", center: true, minHeight: 28));
        _flavorLabel = MakeLabel("--", center: true, minHeight: 28);
        vbox.AddChild(_flavorLabel);

        _bottomLabel = MakeLabel("锅底：--", center: true, minHeight: 28);
        vbox.AddChild(_bottomLabel);

        var sep2 = new HSeparator();
        sep2.CustomMinimumSize = new Vector2(0, 10);
        vbox.AddChild(sep2);

        vbox.AddChild(MakeLabel("抽取食材（选择其一加入锅中）", center: true, minHeight: 28));

        _candidateRow = new HBoxContainer();
        _candidateRow.Alignment = BoxContainer.AlignmentMode.Center;
        _candidateRow.AddThemeConstantOverride("separation", 15);
        _candidateRow.CustomMinimumSize = new Vector2(0, 40);
        vbox.AddChild(_candidateRow);

        _poolCountLabel = MakeLabel("剩余食材池：0", center: true, minHeight: 28);
        vbox.AddChild(_poolCountLabel);

        _skipBowlButton = new Button();
        _skipBowlButton.Text = "跳过本碗（池已空）";
        _skipBowlButton.CustomMinimumSize = new Vector2(180, 36);
        _skipBowlButton.Pressed += OnSkipBowlPressed;
        vbox.AddChild(_skipBowlButton);

        // 普通锅结束后的跨锅推进入口
        _nextPotButton = new Button();
        _nextPotButton.Text = "进入下一锅";
        _nextPotButton.CustomMinimumSize = new Vector2(180, 40);
        _nextPotButton.Pressed += OnNextPotPressed;
        vbox.AddChild(_nextPotButton);

        // 最终锅专用入口：玩家决定「放好」后，整口最终锅一次性结算
        _endCookingButton = new Button();
        _endCookingButton.Text = "结束煮粥（结算最终锅）";
        _endCookingButton.CustomMinimumSize = new Vector2(220, 40);
        _endCookingButton.Pressed += OnEndCookingPressed;
        vbox.AddChild(_endCookingButton);

        _runCompleteLabel = MakeLabel("本局完成", center: true, minHeight: 40);
        _runCompleteLabel.Visible = false;
        vbox.AddChild(_runCompleteLabel);

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

    private void OnCandidateHover(IngredientInstance candidate)
    {
        int? previewBaseScore = null;
        if (_controller.CanSelectIngredient)
        {
            var preview = _controller.PreviewIngredient(candidate);
            previewBaseScore = preview.PreviewBaseScore;
        }

        _tooltipLabel.Text = BuildIngredientTooltip(candidate.Definition, previewBaseScore);

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

    private void OnCandidatePressed(IngredientInstance candidate)
    {
        if (!_controller.CanSelectIngredient)
            return;

        _controller.SelectIngredient(candidate.InstanceId);
        RefreshUI();
    }

    private void OnSkipBowlPressed()
    {
        if (!_controller.CanSkipBowl)
            return;

        _controller.SkipBowl();
        RefreshUI();
    }

    /// <summary>
    /// 普通锅结束后的跨锅推进入口：进入下一锅（或最终锅）并启动。
    /// 只能由 GameController.CanAdvanceToNextPot 判定为真时可用。
    /// </summary>
    private void OnNextPotPressed()
    {
        if (!_controller.CanAdvanceToNextPot)
            return;

        _controller.AdvanceToNextPot();
        RefreshUI();
    }

    /// <summary>
    /// 最终锅结算入口：玩家决定放好后，整口最终锅作为「分数 ×32 的一大碗粥」一次性结算。
    /// 分数由 RunController.EndCooking 内部按 ×32 计算并锁定。
    /// </summary>
    private void OnEndCookingPressed()
    {
        if (!_controller.CanEndCooking)
            return;

        _controller.EndCooking();
        RefreshUI();
    }

    private void RefreshUI()
    {
        var run = _controller.Run;
        var pot = _controller.Pot;

        _chapterLabel.Text = $"第 {run.Chapter} 章";
        _potLabel.Text = run.IsFinalPot ? "最终锅" : $"第 {run.PotIndex} 锅";
        _phaseLabel.Text = $"阶段：{ToBowlPhaseText(pot.CurrentBowlPhase)}";
        // 最终锅不按碗推进（BowlNumber 固定为 ×32 档位），显示碗数会误导玩家
        _bowlLabel.Text = run.IsFinalPot
            ? "最终锅：可无限添加食材"
            : $"碗数：{pot.BowlNumber} / {ToBowlLimitText(pot.BowlLimit)}";
        _scoreLabel.Text = $"基础分：{pot.BaseScore}";
        int multiplier = ScoreCalculator.GetMultiplier(pot.BowlNumber);
        _multiplierLabel.Text = $"倍率：×{multiplier}";
        _finalScoreLabel.Text = pot.IsScoreLocked
            ? $"最终分数：{pot.FinalScore}"
            : run.IsFinalPot
                ? "最终分数：（点「结束煮粥」结算）"
                : "最终分数：（待结算）";
        _totalScoreLabel.Text = $"本锅累计基础分：{pot.TotalBaseScore}";

        var customer = _controller.CurrentCustomer;
        _customerLabel.Text = customer == null
            ? "当前食客：--"
            : customer.Definition.IsRare
                ? $"当前食客：{customer.Definition.Name}（稀有）"
                : $"当前食客：{customer.Definition.Name}";
        _goldLabel.Text = $"金币：{_controller.Player.Gold}";

        _flavorLabel.Text = ToFlavorText(pot);
        _bottomLabel.Text = $"锅底：{ToBottomText(_controller.State.Bottom)}";

        _poolCountLabel.Text = $"剩余食材池：{_controller.RemainingPoolCount}";

        RebuildCandidateButtons();

        bool runComplete = _controller.IsRunComplete;
        _skipBowlButton.Disabled = runComplete || !_controller.CanSkipBowl;
        _nextPotButton.Disabled = runComplete || !_controller.CanAdvanceToNextPot;
        _endCookingButton.Disabled = runComplete || !_controller.CanEndCooking;
        _runCompleteLabel.Visible = runComplete;
    }

    /// <summary>
    /// 按当前候选重建动态食材按钮（0~3 个）。
    /// 表现层只读 CurrentCandidates 并点击回调 GameController，不参与任何流程判断。
    /// </summary>
    private void RebuildCandidateButtons()
    {
        foreach (Node child in _candidateRow.GetChildren())
        {
            _candidateRow.RemoveChild(child);
            child.QueueFree();
        }

        bool canSelect = _controller.CanSelectIngredient;
        foreach (var candidate in _controller.CurrentCandidates)
        {
            var captured = candidate;
            var btn = new Button();
            btn.Text = captured.Definition.Name;
            btn.CustomMinimumSize = new Vector2(80, 36);
            btn.Disabled = !canSelect;
            btn.Pressed += () => OnCandidatePressed(captured);
            btn.MouseEntered += () => OnCandidateHover(captured);
            btn.MouseExited += HideTooltip;
            _candidateRow.AddChild(btn);
        }
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

    private static string ToBottomText(BottomState bottom)
    {
        (string label, FlavorType type)[] flavors =
        [
            ("鲜", FlavorType.Umami),
            ("甜", FlavorType.Sweet),
            ("辣", FlavorType.Spicy),
            ("酸", FlavorType.Sour),
            ("苦", FlavorType.Bitter),
        ];
        var parts = System.Array.ConvertAll(flavors, f => $"{f.label}{bottom.GetFlavor(f.type)}");
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
