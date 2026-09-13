using System.Linq;
using Godot;
using SevenSpices.Core.Companions;
using SevenSpices.Core.Content;
using SevenSpices.Core.Effects;
using SevenSpices.Core.Events;
using SevenSpices.Core.Game;
using SevenSpices.Core.Ingredients;
using SevenSpices.Core.Items;
using SevenSpices.Core.Pot;
using SevenSpices.Core.Save;
using SevenSpices.Core.Scoring;
using SevenSpices.UI;
using SevenSpices.UI.Controls;

namespace SevenSpices;

public partial class Main : Node
{
    // HUD 七味条宽度受限（32px）且边框为偶数 2px，段数过多会导致每段放不下内填充色，
    // 故取 5 段：32px 宽下每段约 5~6px，内填充 1~2px 可见，同时保留可区分的 0..5 档。
    private const int FlavorBarSegments = 5;
    private const string DetailOpenText = "详情 ▾";
    private const string DetailClosedText = "详情 ▸";
    private const string ProfessionConfirmText = "确认重开？";

    private static readonly (string Name, FlavorType Type)[] FlavorRows =
    [
        ("酸", FlavorType.Sour),
        ("甜", FlavorType.Sweet),
        ("苦", FlavorType.Bitter),
        ("辣", FlavorType.Spicy),
        ("鲜", FlavorType.Umami),
        ("咸", FlavorType.Salty),
        ("麻", FlavorType.Numbing),
    ];

    private static readonly Color[] FlavorRowColors =
    [
        UiPalette.FlavorSour,
        UiPalette.FlavorSweet,
        UiPalette.FlavorBitter,
        UiPalette.FlavorSpicy,
        UiPalette.FlavorUmami,
        UiPalette.FlavorSalty,
        UiPalette.FlavorNumbing,
    ];

    private GameController _controller = null!;
    private readonly MetaState _metaState = new();
    private HFlowContainer _candidateRow = null!;
    private Button _skipBowlButton = null!;
    private Label _poolCountLabel = null!;
    private PanelContainer _rewardSection = null!;
    private HFlowContainer _rewardRow = null!;
    private Label _itemCountLabel = null!;
    private HFlowContainer _itemRow = null!;
    private PanelContainer _companionSection = null!;
    private HFlowContainer _companionCandidateRow = null!;
    private Button _skipCompanionButton = null!;
    private VBoxContainer _ownedCompanionList = null!;
    private PanelContainer _shopSection = null!;
    private HFlowContainer _shopRow = null!;
    private Button _skipShopButton = null!;
    private PanelContainer _routeSection = null!;
    private HFlowContainer _routeRow = null!;
    private Button _skipRouteButton = null!;

    private Label _chapterLabel = null!;
    private Label _potLabel = null!;
    private Label _professionLabel = null!;
    private HFlowContainer _professionRow = null!;
    private Label _phaseLabel = null!;
    private Label _bowlLabel = null!;
    private Label _scoreLabel = null!;
    private Label _flavorScoreLabel = null!;
    private Label _multiplierLabel = null!;
    private Label _finalScoreLabel = null!;
    private Label _totalScoreLabel = null!;
    private Label _customerLabel = null!;
    private Label _bossLabel = null!;
    private Label _bossLineLabel = null!;
    private Label _bossVerdictLabel = null!;
    private Label _goldLabel = null!;
    private PixelBar[] _flavorBars = [];
    private Label[] _flavorValueLabels = [];
    private Label _flavorEntropyLabel = null!;
    private Label _flavorStatusLabel = null!;
    private Label _bottomLabel = null!;

    // 常驻 HUD（固定不滚动）与按需详情抽屉
    private Label _hudScoreLabel = null!;
    private Button _detailToggleButton = null!;
    private PanelContainer _detailDrawer = null!;
    private bool _detailOpen;
    private Label _runEndLabel = null!;
    private Button _restartButton = null!;
    private Button _nextPotButton = null!;
    private Button _endCookingButton = null!;
    private Button _saveButton = null!;
    private Button _loadButton = null!;
    private Label _saveFeedbackLabel = null!;

    // 主体滚动容器与「自动定位」状态：仅当最需要操作的区块发生变化时滚动一次，
    // 避免每次 RefreshUI 都覆盖玩家手动滚动的位置。
    private ScrollContainer _scroll = null!;
    private Control? _pendingScrollTarget;
    private Control? _lastScrollTarget;

    // 职业重开二次确认：记录当前待确认的职业 Id 与其按钮，避免误点立刻丢进度。
    private string? _pendingProfessionId;
    private readonly System.Collections.Generic.List<(string Id, Button Button, string Name)> _professionButtons = new();

    // 事件驱动刷新：任一游戏事件置脏，下一帧统一 RefreshUI。
    private bool _uiDirty;

    // 动态 tooltip 面板（顶层覆盖，不参与布局，不拦截鼠标）
    private PanelContainer _tooltipPanel = null!;
    private Label _tooltipLabel = null!;
    private bool _tooltipNeedsReposition;   // 等待下一帧取到真实 Size 后定位
    private Vector2 _tooltipAnchor;         // 期望放置的初始锚点（鼠标附近）

    public override void _Ready()
    {
        GetTree().Root.Theme = PixelTheme.Build();
        GetWindow().MinSize = new Vector2I(UiMetrics.BaseWidth, UiMetrics.BaseHeight);

        _controller = new GameController(metaState: _metaState);
        _controller.StartNewGame();

        GD.Print("七荤八素启动");
        GD.Print($"Chapter: {_controller.State.Run.Chapter}");
        GD.Print($"Pot: {_controller.State.Run.PotIndex}");
        GD.Print($"Final Pot: {_controller.State.Run.IsFinalPot}");
        GD.Print($"Phase: {_controller.State.Pot.Phase}");

        BuildUI();
        RefreshUI();

        // 核心系统在关键节点发布事件；表现层只监听并置脏，不反查流程。
        _controller.Events.Subscribe(OnGameEvent);
    }

    private void OnGameEvent(GameEvent gameEvent)
    {
        _uiDirty = true;

        // 任何真实的游戏状态变化都取消「职业重开」的待确认态，避免旧的确认残留造成误重开。
        if (_pendingProfessionId != null)
        {
            _pendingProfessionId = null;
            UpdateProfessionButtons();
        }
    }

    private void BuildUI()
    {
        var uiRoot = new Control();
        uiRoot.AnchorRight = 1.0f;
        uiRoot.AnchorBottom = 1.0f;
        uiRoot.GrowHorizontal = Control.GrowDirection.Both;
        uiRoot.GrowVertical = Control.GrowDirection.Both;
        // Main 的场景根是 Node，主题挂在 Root Window 上不会传播到运行期创建的 Control；
        // 因此所有覆盖层（含 tooltip）都必须挂在 uiRoot 之下，才能一并继承像素皮肤。
        uiRoot.Theme = GetTree().Root.Theme;
        AddChild(uiRoot);

        var background = new ColorRect { Color = UiPalette.Bg };
        background.AnchorRight = 1.0f;
        background.AnchorBottom = 1.0f;
        background.GrowHorizontal = Control.GrowDirection.Both;
        background.GrowVertical = Control.GrowDirection.Both;
        background.MouseFilter = Control.MouseFilterEnum.Ignore;
        uiRoot.AddChild(background);

        var margin = new MarginContainer();
        margin.AnchorRight = 1.0f;
        margin.AnchorBottom = 1.0f;
        margin.GrowHorizontal = Control.GrowDirection.Both;
        margin.GrowVertical = Control.GrowDirection.Both;
        margin.AddThemeConstantOverride("margin_left", UiMetrics.Pad);
        margin.AddThemeConstantOverride("margin_top", UiMetrics.Pad);
        margin.AddThemeConstantOverride("margin_right", UiMetrics.Pad);
        margin.AddThemeConstantOverride("margin_bottom", UiMetrics.Pad);
        uiRoot.AddChild(margin);

        // 三层结构：常驻 HUD（固定不滚动）／按需详情抽屉（HUD 下方展开）／可滚动主体。
        // 见《界面风格规范》§六：常驻 = 分数、倍率、七味条；细节按需展开。
        var rootCol = new VBoxContainer();
        rootCol.AddThemeConstantOverride("separation", UiMetrics.Gap);
        margin.AddChild(rootCol);

        BuildHud(rootCol);

        // 详情抽屉（默认收起）：承载「味道种类数 / 熵倍率」「陈酿 / 固化 / 臭」「锅底」等按需信息。
        // 放在 HUD 与滚动主体之间，展开时压缩滚动区高度而不把主体挤出屏幕。
        _detailDrawer = MakePanel("PanelCard");
        _detailDrawer.Visible = false;
        rootCol.AddChild(_detailDrawer);

        var drawerCol = MakeColumn(UiMetrics.Gap);
        _detailDrawer.AddChild(drawerCol);
        drawerCol.AddChild(MakeLabel("按需详情（味道种类 / 状态 / 锅底）", center: true, minHeight: 24, variation: "LabelDim"));

        _flavorEntropyLabel = MakeLabel("味道种类：0", center: true, minHeight: 24, variation: "LabelDim");
        drawerCol.AddChild(_flavorEntropyLabel);

        _flavorStatusLabel = MakeLabel(string.Empty, center: true, minHeight: 24, variation: "LabelDim");
        drawerCol.AddChild(_flavorStatusLabel);

        _bottomLabel = MakeLabel("锅底：--", center: true, minHeight: 24, variation: "LabelDim");
        drawerCol.AddChild(_bottomLabel);

        // 操作区放入纵向滚动容器，避免区块超出窗口高度后底部按钮被裁剪到屏幕外。
        // 各行动态按钮改用 HFlowContainer 自动换行，故横向滚动保持 Disabled 即可。
        _scroll = new ScrollContainer();
        _scroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        rootCol.AddChild(_scroll);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", UiMetrics.Gap);
        vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _scroll.AddChild(vbox);

        // 主体区块按「当前需操作 → 锅末流程 → 信息 → 档位与元操作」排序（见《界面风格规范》§六）：
        // 让本碗主操作（选食材 / 道具 / 推进）落在首屏，不必先滚动。
        // 原顶部大标题「七荤八素」已移除：常驻 HUD 已提供顶部锚点，避免挤占首屏高度。

        // ── 1. 当前需操作：食材候选（3 选 1）+ 倒水 ──────────────────────────────
        var candidatePanel = MakePanel("PanelPlate");
        vbox.AddChild(candidatePanel);
        var candidateCol = MakeColumn(UiMetrics.Gap);
        candidatePanel.AddChild(candidateCol);

        candidateCol.AddChild(MakeLabel("抽取食材（选择其一加入锅中）", center: true, minHeight: 24, variation: "LabelDim"));

        _candidateRow = MakeFlowRow();
        candidateCol.AddChild(_candidateRow);

        _poolCountLabel = MakeLabel("剩余食材池：0", center: true, minHeight: 24);
        candidateCol.AddChild(_poolCountLabel);

        _skipBowlButton = new Button();
        _skipBowlButton.Text = "倒水（本碗不加入食材，直接结算）";
        _skipBowlButton.CustomMinimumSize = new Vector2(280, 32);
        _skipBowlButton.Pressed += OnSkipBowlPressed;
        candidateCol.AddChild(_skipBowlButton);

        // ── 1. 当前需操作：道具 ──────────────────────────────────────────────────
        var itemPanel = MakePanel("PanelCard");
        vbox.AddChild(itemPanel);
        var itemCol = MakeColumn(UiMetrics.Gap);
        itemPanel.AddChild(itemCol);

        itemCol.AddChild(MakeLabel("道具（加入食材前使用）", center: true, minHeight: 24, variation: "LabelDim"));

        _itemCountLabel = MakeLabel("持有道具：0", center: true, minHeight: 24);
        itemCol.AddChild(_itemCountLabel);

        _itemRow = MakeFlowRow();
        itemCol.AddChild(_itemRow);

        // ── 1. 当前需操作：碗 / 锅推进按钮 ──────────────────────────────────────
        // 普通锅结束后的跨锅推进入口
        _nextPotButton = new Button();
        _nextPotButton.Text = "进入下一锅";
        _nextPotButton.CustomMinimumSize = new Vector2(176, 32);
        _nextPotButton.Pressed += OnNextPotPressed;
        vbox.AddChild(_nextPotButton);

        // 最终锅专用入口：玩家决定「放好」后，整口最终锅一次性结算
        _endCookingButton = new Button();
        _endCookingButton.Text = "结束煮粥（结算最终锅）";
        _endCookingButton.CustomMinimumSize = new Vector2(224, 32);
        _endCookingButton.Pressed += OnEndCookingPressed;
        vbox.AddChild(_endCookingButton);

        // ── 2. 锅末流程：奖励 → 伙伴候选 → 已有伙伴 → 商店 → 路线（串联顺序不变）──

        // 普通锅结束后的 X 选 1 食材奖励区域（仅奖励态显示）
        _rewardSection = MakePanel("PanelCard");
        _rewardSection.Visible = false;
        vbox.AddChild(_rewardSection);

        var rewardCol = MakeColumn(UiMetrics.Gap);
        _rewardSection.AddChild(rewardCol);
        rewardCol.AddChild(MakeLabel("锅结束奖励：选择 1 个食材", center: true, minHeight: 24, variation: "LabelDim"));

        _rewardRow = MakeFlowRow();
        rewardCol.AddChild(_rewardRow);

        // 普通锅结束后的伙伴候选区域（仅等待选择时显示）
        _companionSection = MakePanel("PanelCard");
        _companionSection.Visible = false;
        vbox.AddChild(_companionSection);

        var companionCol = MakeColumn(UiMetrics.Gap);
        _companionSection.AddChild(companionCol);

        companionCol.AddChild(MakeLabel("伙伴候选：选择 1 位伙伴", center: true, minHeight: 24, variation: "LabelDim"));

        _companionCandidateRow = MakeFlowRow();
        companionCol.AddChild(_companionCandidateRow);

        _skipCompanionButton = new Button();
        _skipCompanionButton.Text = "跳过伙伴选择";
        _skipCompanionButton.CustomMinimumSize = new Vector2(176, 32);
        _skipCompanionButton.Pressed += OnSkipCompanionPressed;
        companionCol.AddChild(_skipCompanionButton);

        // 已有伙伴（长期资源，只读展示）
        var ownedPanel = MakePanel("PanelCard");
        vbox.AddChild(ownedPanel);
        var ownedCol = MakeColumn(UiMetrics.Gap);
        ownedPanel.AddChild(ownedCol);
        ownedCol.AddChild(MakeLabel("已有伙伴", center: true, minHeight: 24, variation: "LabelDim"));

        _ownedCompanionList = new VBoxContainer();
        _ownedCompanionList.AddThemeConstantOverride("separation", UiMetrics.Gap);
        ownedCol.AddChild(_ownedCompanionList);

        // 普通锅结束后的商店区域（仅营业时显示）
        _shopSection = MakePanel("PanelCard");
        _shopSection.Visible = false;
        vbox.AddChild(_shopSection);

        var shopCol = MakeColumn(UiMetrics.Gap);
        _shopSection.AddChild(shopCol);

        shopCol.AddChild(MakeLabel("商店：购买食材 / 道具", center: true, minHeight: 24, variation: "LabelDim"));

        _shopRow = MakeFlowRow();
        shopCol.AddChild(_shopRow);

        _skipShopButton = new Button();
        _skipShopButton.Text = "跳过商店";
        _skipShopButton.CustomMinimumSize = new Vector2(176, 32);
        _skipShopButton.Pressed += OnSkipShopPressed;
        shopCol.AddChild(_skipShopButton);

        // 每章第 3 锅商店结算后的路线（餐饮风潮）区域（仅等待选择时显示）
        _routeSection = MakePanel("PanelCard");
        _routeSection.Visible = false;
        vbox.AddChild(_routeSection);

        var routeCol = MakeColumn(UiMetrics.Gap);
        _routeSection.AddChild(routeCol);

        routeCol.AddChild(MakeLabel(
            "监味星君指点：挑一条道（1 保底 + 2 风潮）", center: true, minHeight: 24, variation: "LabelDim"));

        _routeRow = MakeFlowRow();
        routeCol.AddChild(_routeRow);

        _skipRouteButton = new Button();
        _skipRouteButton.Text = "跳过（领保底）";
        _skipRouteButton.CustomMinimumSize = new Vector2(176, 32);
        _skipRouteButton.Pressed += OnSkipRoutePressed;
        routeCol.AddChild(_skipRouteButton);

        // ── 3. 信息区：分数明细 / 食客 / 饕餮 / 结算文案 / 金币 ──────────────────
        var infoPanel = MakePanel("PanelPlate");
        vbox.AddChild(infoPanel);
        var infoCol = MakeColumn(UiMetrics.Gap);
        infoPanel.AddChild(infoCol);

        // 章 / 锅 / 碗数 / 阶段与「分数 + 倍率」已上移到常驻 HUD（见 BuildHud），此处只留分数明细。
        _scoreLabel = MakeLabel("基础分：0", center: true, minHeight: 24);
        infoCol.AddChild(_scoreLabel);
        _flavorScoreLabel = MakeLabel("味道分：0", center: true, minHeight: 24);
        infoCol.AddChild(_flavorScoreLabel);
        _finalScoreLabel = MakeLabel("最终分数：0", center: true, minHeight: 24);
        infoCol.AddChild(_finalScoreLabel);
        _totalScoreLabel = MakeLabel("本锅累计基础分：0", center: true, minHeight: 24);
        infoCol.AddChild(_totalScoreLabel);
        _customerLabel = MakeLabel("当前食客：--", center: true, minHeight: 24, variation: "LabelDim");
        infoCol.AddChild(_customerLabel);

        // 饕餮试吃区：仅当前食客是饕餮时显示，文案/台词全部取自 BossConfig。
        _bossLabel = MakeLabel("饕餮：--", center: true, minHeight: 24);
        _bossLabel.Visible = false;
        infoCol.AddChild(_bossLabel);

        _bossLineLabel = MakeLabel(string.Empty, center: true, minHeight: 24, variation: "LabelDim");
        _bossLineLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _bossLineLabel.Visible = false;
        infoCol.AddChild(_bossLineLabel);

        // 最近一次试吃判定（章末 Boss / 最终锅真身共用），无记录时隐藏。
        _bossVerdictLabel = MakeLabel(string.Empty, center: true, minHeight: 24, variation: "LabelDim");
        _bossVerdictLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _bossVerdictLabel.Visible = false;
        infoCol.AddChild(_bossVerdictLabel);

        // 本局终止 / 终局文案：放在信息区靠上位置，避免被长滚动区裁到视口外。
        _runEndLabel = MakeLabel(string.Empty, center: true, minHeight: 56);
        _runEndLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _runEndLabel.Visible = false;
        infoCol.AddChild(_runEndLabel);

        // 本局结束后的重开入口：仅在 IsRunComplete 时可见 / 可点。
        _restartButton = new Button();
        _restartButton.Text = "重新开始（投胎重来）";
        _restartButton.CustomMinimumSize = new Vector2(224, 32);
        _restartButton.Visible = false;
        _restartButton.Pressed += OnRestartPressed;
        infoCol.AddChild(_restartButton);

        _goldLabel = MakeLabel("金币：0", center: true, minHeight: 24);
        infoCol.AddChild(_goldLabel);

        // 七味条已上移常驻 HUD；味道种类 / 陈酿 / 固化 / 臭 / 锅底已移入详情抽屉（见 BuildUI 顶部）。

        // ── 4. 档位与元操作：职业 / 存档读档 ────────────────────────────────────

        // 职业选择（7 个职业按钮在窄屏下自动换行）：进行中点击需二次确认才会重开本局。
        var professionPanel = MakePanel("PanelPlate");
        vbox.AddChild(professionPanel);
        var professionCol = MakeColumn(UiMetrics.Gap);
        professionPanel.AddChild(professionCol);

        _professionLabel = MakeLabel("职业：--", center: true, minHeight: 24, variation: "LabelDim");
        professionCol.AddChild(_professionLabel);

        _professionRow = MakeFlowRow();
        professionCol.AddChild(_professionRow);

        foreach (var profession in ProfessionConfig.All)
        {
            var captured = profession;
            var professionButton = new Button();
            professionButton.Text = captured.Name;
            professionButton.CustomMinimumSize = new Vector2(112, 32);
            professionButton.TooltipText = captured.Description;
            professionButton.Pressed += () => OnProfessionPressed(captured.Id);
            _professionRow.AddChild(professionButton);
            _professionButtons.Add((captured.Id, professionButton, captured.Name));
        }

        // 存档 / 读档（随时可用，读档会从当前锅开头重新开始）
        var savePanel = MakePanel("PanelCard");
        vbox.AddChild(savePanel);
        var saveCol = MakeColumn(UiMetrics.Gap);
        savePanel.AddChild(saveCol);

        var saveRow = new HBoxContainer();
        saveRow.Alignment = BoxContainer.AlignmentMode.Center;
        saveRow.AddThemeConstantOverride("separation", UiMetrics.Gap);
        saveCol.AddChild(saveRow);

        _saveButton = new Button();
        _saveButton.Text = "存档";
        _saveButton.CustomMinimumSize = new Vector2(112, 32);
        _saveButton.Pressed += OnSavePressed;
        saveRow.AddChild(_saveButton);

        _loadButton = new Button();
        _loadButton.Text = "读档";
        _loadButton.CustomMinimumSize = new Vector2(112, 32);
        _loadButton.Pressed += OnLoadPressed;
        saveRow.AddChild(_loadButton);

        _saveFeedbackLabel = MakeLabel(string.Empty, center: true, minHeight: 24, variation: "LabelDim");
        _saveFeedbackLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        saveCol.AddChild(_saveFeedbackLabel);

        // tooltip 挂到 uiRoot 的顶层覆盖 Control，脱离 vbox 布局流并继承 uiRoot 主题（像素皮肤）；
        // MouseFilter.Ignore 让它彻底不参与鼠标事件，防止闪烁。
        var tooltipOverlay = new Control();
        tooltipOverlay.AnchorRight = 1.0f;
        tooltipOverlay.AnchorBottom = 1.0f;
        tooltipOverlay.GrowHorizontal = Control.GrowDirection.Both;
        tooltipOverlay.GrowVertical = Control.GrowDirection.Both;
        tooltipOverlay.MouseFilter = Control.MouseFilterEnum.Ignore;
        uiRoot.AddChild(tooltipOverlay);

        _tooltipPanel = new PanelContainer { ThemeTypeVariation = "PanelTooltip" };
        _tooltipPanel.Visible = false;
        _tooltipPanel.MouseFilter = Control.MouseFilterEnum.Ignore;
        tooltipOverlay.AddChild(_tooltipPanel);

        _tooltipLabel = new Label();
        _tooltipLabel.AutowrapMode = TextServer.AutowrapMode.Off;
        _tooltipLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
        _tooltipPanel.AddChild(_tooltipLabel);
    }

    /// <summary>
    /// 常驻 HUD（固定不滚动）：分数 + 倍率、章 / 锅 / 碗数 / 阶段、七味短条、详情开合按钮。
    /// 依据《界面风格规范》§六「常驻精简」；纵向刻意收紧（≈104px），给下方滚动主体留空间。
    /// </summary>
    private void BuildHud(Container parent)
    {
        var hud = MakePanel("PanelPlate");
        parent.AddChild(hud);

        var hudCol = MakeColumn(UiMetrics.Gap);
        hud.AddChild(hudCol);

        // 第一行：分数（标题字号，醒目）+ 倍率
        var scoreRow = new HBoxContainer();
        scoreRow.Alignment = BoxContainer.AlignmentMode.Center;
        scoreRow.AddThemeConstantOverride("separation", UiMetrics.Pad);
        hudCol.AddChild(scoreRow);

        _hudScoreLabel = MakeLabel("分数 0", center: true, variation: "LabelTitle");
        scoreRow.AddChild(_hudScoreLabel);

        _multiplierLabel = MakeLabel("倍率：×1", center: true, minHeight: 24);
        scoreRow.AddChild(_multiplierLabel);

        // 第二行：章 / 锅 / 碗数 / 阶段（核心进度，紧凑一行）+ 右侧详情开关
        var progressRow = new HBoxContainer();
        progressRow.AddThemeConstantOverride("separation", UiMetrics.Pad);
        hudCol.AddChild(progressRow);

        _chapterLabel = MakeLabel("第 1 章", center: true, minHeight: 24);
        progressRow.AddChild(_chapterLabel);
        _potLabel = MakeLabel("第 1 锅", center: true, minHeight: 24);
        progressRow.AddChild(_potLabel);
        _bowlLabel = MakeLabel("碗数：-- / --", center: true, minHeight: 24);
        progressRow.AddChild(_bowlLabel);
        _phaseLabel = MakeLabel("阶段：--", center: true, minHeight: 24);
        progressRow.AddChild(_phaseLabel);

        var spacer = new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        progressRow.AddChild(spacer);

        _detailToggleButton = new Button { Text = DetailClosedText };
        _detailToggleButton.CustomMinimumSize = new Vector2(88, 32);
        _detailToggleButton.Pressed += OnDetailTogglePressed;
        progressRow.AddChild(_detailToggleButton);

        // 第三行：七味短条（单字味道名 + 短条 + 数值），HFlowContainer 自动换行防溢出
        var flavorFlow = MakeFlowRow();
        flavorFlow.AddThemeConstantOverride("h_separation", UiMetrics.Unit / 2);
        flavorFlow.AddThemeConstantOverride("v_separation", UiMetrics.Unit / 2);
        hudCol.AddChild(flavorFlow);

        _flavorBars = new PixelBar[FlavorRows.Length];
        _flavorValueLabels = new Label[FlavorRows.Length];
        for (int i = 0; i < FlavorRows.Length; i++)
        {
            var chip = new HBoxContainer();
            chip.AddThemeConstantOverride("separation", UiMetrics.Unit / 2);

            var flavorName = MakeLabel(FlavorRows[i].Name, center: true);
            flavorName.CustomMinimumSize = new Vector2(16, 0);
            chip.AddChild(flavorName);

            var bar = new PixelBar
            {
                SegmentCount = FlavorBarSegments,
                SegmentGap = 1,
                Filled = 0,
                FillColor = FlavorRowColors[i],
            };
            bar.CustomMinimumSize = new Vector2(32, UiMetrics.Unit);
            chip.AddChild(bar);
            _flavorBars[i] = bar;

            var flavorValue = MakeLabel("0");
            flavorValue.CustomMinimumSize = new Vector2(24, 0);
            flavorValue.HorizontalAlignment = HorizontalAlignment.Right;
            flavorValue.VerticalAlignment = VerticalAlignment.Center;
            chip.AddChild(flavorValue);
            _flavorValueLabels[i] = flavorValue;

            flavorFlow.AddChild(chip);
        }
    }

    /// <summary>
    /// 详情抽屉开合：只切 Visible 与按钮文案，不影响游戏状态，也无需置脏刷新。
    /// 视为「先不重开」的操作，顺带清除职业重开的待确认态。
    /// </summary>
    private void OnDetailTogglePressed()
    {
        _detailOpen = !_detailOpen;
        _detailDrawer.Visible = _detailOpen;
        _detailToggleButton.Text = _detailOpen ? DetailOpenText : DetailClosedText;

        _pendingProfessionId = null;
        UpdateProfessionButtons();
    }

    private static Label MakeLabel(string text, bool center = false, int minHeight = 0, string? variation = null)
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
        if (variation != null)
            lbl.ThemeTypeVariation = variation;
        return lbl;
    }

    private static PanelContainer MakePanel(string variation) =>
        new() { ThemeTypeVariation = variation };

    private static VBoxContainer MakeColumn(int separation)
    {
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", separation);
        return col;
    }

    /// <summary>会换行的横向按钮行：窄屏（640×360）下自动折行，避免按钮被裁到屏幕外。</summary>
    private static HFlowContainer MakeFlowRow()
    {
        var row = new HFlowContainer();
        row.Alignment = FlowContainer.AlignmentMode.Center;
        row.AddThemeConstantOverride("h_separation", UiMetrics.Gap);
        row.AddThemeConstantOverride("v_separation", UiMetrics.Gap);
        row.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        return row;
    }

    private void OnCandidateHover(IngredientInstance candidate)
    {
        IngredientPreview? preview = null;
        if (_controller.CanSelectIngredient)
            preview = _controller.PreviewIngredient(candidate);

        ShowIngredientTooltip(candidate, preview);
    }

    /// <summary>
    /// 奖励候选悬停：奖励候选不属于本锅抽取池，不做投入预测，只显示 Definition 详情。
    /// </summary>
    private void OnRewardHover(IngredientInstance candidate)
    {
        ShowIngredientTooltip(candidate, null);
    }

    private void ShowIngredientTooltip(IngredientInstance candidate, IngredientPreview? preview)
    {
        _tooltipLabel.Text = BuildIngredientTooltip(candidate.Definition, preview);

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
        // 上一帧 RefreshUI 置的自动定位请求：等布局完成后再滚动，避免用旧尺寸计算。
        if (_pendingScrollTarget != null)
        {
            _scroll.EnsureControlVisible(_pendingScrollTarget);
            _pendingScrollTarget = null;
        }

        if (_uiDirty)
        {
            _uiDirty = false;
            RefreshUI();
        }

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
        _uiDirty = true;
    }

    /// <summary>
    /// 锅结束奖励选择：选定后食材进入食材篮，并解除「进入下一锅」的门控。
    /// 只转发，不做任何流程判断。
    /// </summary>
    private void OnRewardPressed(IngredientInstance candidate)
    {
        if (!_controller.CanChooseReward)
            return;

        _controller.ChooseReward(candidate.InstanceId);
        _uiDirty = true;
    }

    /// <summary>伙伴候选选择：只转发 GameController.ChooseCompanion，随后刷新。</summary>
    private void OnCompanionPressed(CompanionDefinition candidate)
    {
        if (!_controller.CanChooseCompanion)
            return;

        _controller.ChooseCompanion(candidate.Id);
        _uiDirty = true;
    }

    /// <summary>跳过伙伴选择：只转发 GameController.SkipCompanionChoice。</summary>
    private void OnSkipCompanionPressed()
    {
        if (!_controller.CanSkipCompanionChoice)
            return;

        _controller.SkipCompanionChoice();
        _uiDirty = true;
    }

    /// <summary>商店购买：只转发 GameController.Buy，不做任何流程判断。</summary>
    private void OnBuyPressed(int offerIndex)
    {
        if (!_controller.CanBuy(offerIndex))
            return;

        _controller.Buy(offerIndex);
        _uiDirty = true;
    }

    /// <summary>跳过商店：只转发 GameController.SkipShop。</summary>
    private void OnSkipShopPressed()
    {
        if (!_controller.CanSkipShop)
            return;

        _controller.SkipShop();
        _uiDirty = true;
    }

    /// <summary>监味星君路线候选选择：只转发 GameController.ChooseRoute，不做流程判断。</summary>
    private void OnRoutePressed(RouteDefinition candidate)
    {
        if (!_controller.CanChooseRoute)
            return;

        _controller.ChooseRoute(candidate.Id);
        _uiDirty = true;
    }

    /// <summary>跳过路线：只转发 GameController.SkipRoute（跳过＝自动领保底）。</summary>
    private void OnSkipRoutePressed()
    {
        if (!_controller.CanSkipRoute)
            return;

        _controller.SkipRoute();
        _uiDirty = true;
    }

    private void OnSkipBowlPressed()
    {
        if (!_controller.CanSkipBowl)
            return;

        _controller.SkipBowl();
        _uiDirty = true;
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
        _uiDirty = true;
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
        _uiDirty = true;
    }

    /// <summary>
    /// 重新开始（投胎重来）入口：仅在本局已结束时可点，沿用当前职业。
    /// </summary>
    private void OnRestartPressed()
    {
        if (!_controller.IsRunComplete)
            return;

        _controller.StartNewGame(_controller.Run.ProfessionId);
        _uiDirty = true;
    }

    /// <summary>
    /// 存档：把 GameController 抓取的 DTO 序列化后写入本地文件，并给一句反馈。
    /// 写文件失败由 SaveStore 返回 false，据此提示「存档失败」，不崩游戏。
    /// 视为「先不重开」的操作，顺带清除职业重开的待确认态。
    /// </summary>
    private void OnSavePressed()
    {
        _pendingProfessionId = null;
        UpdateProfessionButtons();

        try
        {
            bool ok = SaveStore.Write(SaveSerializer.ToJson(_controller.CaptureSave()));
            SetSaveFeedback(ok ? "已存档。" : "存档失败：无法写入存档文件。");
        }
        catch (Exception ex)
        {
            SetSaveFeedback($"存档失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 读档：无存档 / 文件损坏 / 反序列化失败分别提示；成功则让 GameController 恢复
    /// （从当前锅开头重新开始），随后置脏刷新 UI。任何失败都不崩游戏。
    /// </summary>
    private void OnLoadPressed()
    {
        SaveReadResult result = SaveStore.Read(out string? json);
        if (result == SaveReadResult.NotFound)
        {
            SetSaveFeedback("没有找到存档。");
            return;
        }
        if (result == SaveReadResult.Failed)
        {
            SetSaveFeedback("读档失败：存档文件无法读取。");
            return;
        }

        try
        {
            _controller.RestoreSave(SaveSerializer.FromJson(json!));
            SetSaveFeedback(_controller.IsRunComplete
                ? "读档完成：本局已结束。"
                : "读档完成：已从当前锅开头重新开始。");
            _uiDirty = true;
        }
        catch (Exception ex)
        {
            SetSaveFeedback($"读档失败：{ex.Message}");
        }
    }

    private void SetSaveFeedback(string text)
    {
        _saveFeedbackLabel.Text = text;
        _saveFeedbackLabel.Visible = text.Length > 0;
    }

    /// <summary>
    /// 职业选择入口：进行中点击职业会重开本局，故加一道「二次点击确认」防误点。
    /// 首次点击只把该按钮切成「确认重开？」，再点一次同一按钮才会真正重开；
    /// 期间发生任何游戏事件（<see cref="OnGameEvent"/>）都会取消待确认态。
    /// 表现层只转发 GameController.StartNewGame，不参与任何流程判断。
    /// </summary>
    private void OnProfessionPressed(string professionId)
    {
        if (_pendingProfessionId != professionId)
        {
            _pendingProfessionId = professionId;
            UpdateProfessionButtons();
            return;
        }

        _pendingProfessionId = null;
        UpdateProfessionButtons();
        _controller.StartNewGame(professionId);
        _uiDirty = true;
    }

    /// <summary>职业按钮文案：待确认的那个显示「确认重开？」，其余恢复职业名。</summary>
    private void UpdateProfessionButtons()
    {
        foreach (var (id, button, name) in _professionButtons)
            button.Text = id == _pendingProfessionId ? ProfessionConfirmText : name;
    }

    /// <summary>
    /// 依据当前状态挑出「最需要用户操作」的区块；只在目标区块发生变化时请求滚动，
    /// 避免每次 RefreshUI 都把玩家手动滚动的位置重置回去。未激活的区块不参与定位。
    /// </summary>
    private Control? ResolveScrollTarget()
    {
        if (_controller.IsRunComplete)
            return _restartButton.Visible ? _restartButton : null;

        // 锅末流程严格串行：奖励 → 伙伴 → 商店 → 路线，同一时刻至多一个激活。
        if (_controller.IsAwaitingReward)
            return _rewardSection;
        if (_controller.IsAwaitingCompanionChoice)
            return _companionSection;
        if (_controller.IsShopOpen)
            return _shopSection;
        if (_controller.IsAwaitingRouteChoice)
            return _routeSection;

        // 本碗主操作优先于跨锅推进：最终锅全程可结束煮粥，但不能因此把选食材挤出视野。
        if (_controller.CanSelectIngredient)
            return _candidateRow;
        if (_controller.CanSkipBowl)
            return _skipBowlButton;
        if (_controller.CanEndCooking)
            return _endCookingButton;
        if (_controller.CanAdvanceToNextPot)
            return _nextPotButton;

        return null;
    }

    private void RefreshUI()
    {
        var run = _controller.Run;
        var pot = _controller.Pot;

        _chapterLabel.Text = $"第 {run.Chapter} 章";
        _potLabel.Text = run.IsFinalPot ? "最终锅" : $"第 {run.PotIndex} 锅";
        _professionLabel.Text = ProfessionConfig.TryGet(run.ProfessionId, out var profession)
            ? $"职业：{profession.Name}（{ToFlavorName(profession.Theme)}）"
            : "职业：--";
        _phaseLabel.Text = $"阶段：{ToBowlPhaseText(pot.CurrentBowlPhase)}";
        // 最终锅不按碗推进（BowlNumber 固定为 ×32 档位），显示碗数会误导玩家
        _bowlLabel.Text = run.IsFinalPot
            ? "最终锅：可无限添加食材"
            : $"碗数：{pot.BowlNumber} / {ToBowlLimitText(pot.BowlLimit)}";
        _scoreLabel.Text = $"基础分：{(int)Math.Floor(pot.BaseScoreWithFlavor)}（食材/效果分：{pot.BaseScore}）";
        _hudScoreLabel.Text = $"分数 {(int)Math.Floor(pot.BaseScoreWithFlavor)}";
        _flavorScoreLabel.Text = $"味道分：{(int)Math.Floor(pot.FlavorScore)}";
        int multiplier = ScoreCalculator.GetEffectiveMultiplier(pot);
        _multiplierLabel.Text = pot.HeatBowlsRemaining > 0 && pot.HeatBonusTiers > 0
            ? $"倍率：×{multiplier}（余温 +{pot.HeatBonusTiers} 档，剩 {pot.HeatBowlsRemaining} 碗）"
            : $"倍率：×{multiplier}";
        if (pot.IsScoreLocked)
        {
            _finalScoreLabel.Text = $"最终分数：{pot.FinalScore}";
        }
        else if (run.Outcome != RunOutcome.Unsettled)
        {
            // 最终锅已结算、但锅状态被重置（读档恢复终局）：从 Boss 记录回读已结算分数。
            var settledFinal = run.ChapterBossRecords.LastOrDefault(r => r.IsFinalPot);
            _finalScoreLabel.Text = settledFinal != null
                ? $"最终分数：{settledFinal.PotTotalFinalScore}（已结算）"
                : "最终分数：（点「结束煮粥」结算）";
        }
        else if (run.IsFinalPot)
        {
            _finalScoreLabel.Text = "最终分数：（点「结束煮粥」结算）";
        }
        else
        {
            _finalScoreLabel.Text = "最终分数：（待结算）";
        }
        _totalScoreLabel.Text = $"本锅累计基础分：{pot.TotalBaseScore}";

        var customer = _controller.CurrentCustomer;
        var bossForm = customer != null ? BossConfig.Default.FindForm(customer.Definition.Id) : null;

        // 最终锅不逐碗指派食客，进行中直接展示真身（名称/台词来自 BossConfig.Default.TrueForm）。
        if (bossForm == null && run.IsFinalPot && pot.Phase == PotPhase.InProgress)
            bossForm = BossConfig.Default.TrueForm;

        if (customer == null && bossForm != null)
            _customerLabel.Text = $"当前食客：{bossForm.Name}（最终锅待试）";
        else if (customer == null)
            _customerLabel.Text = "当前食客：--";
        else if (bossForm != null)
            _customerLabel.Text = $"当前食客：{bossForm.Name}";
        else if (customer.Definition.IsRare)
            _customerLabel.Text = $"当前食客：{customer.Definition.Name}（稀有）";
        else
            _customerLabel.Text = $"当前食客：{customer.Definition.Name}";

        bool bossPresent = bossForm != null;
        _bossLabel.Visible = bossPresent;
        _bossLineLabel.Visible = bossPresent && bossForm!.Lines.Count > 0;
        if (bossPresent)
        {
            _bossLabel.Text = $"饕餮试吃中（达标门槛：本锅累计最终分 ≥ {bossForm!.SatisfyThreshold}）";
            _bossLineLabel.Text = string.Join("\n", bossForm.Lines);
        }

        RefreshBossVerdict(run);
        _goldLabel.Text = $"金币：{_controller.Player.Gold}";

        RefreshFlavorBars(pot);
        _flavorEntropyLabel.Text = BuildFlavorEntropyText(pot);
        string flavorStatus = BuildFlavorStatusText(pot);
        _flavorStatusLabel.Text = flavorStatus;
        _flavorStatusLabel.Visible = flavorStatus.Length > 0;
        _bottomLabel.Text = $"锅底：{ToBottomText(_controller.State.Bottom)}";

        _poolCountLabel.Text = $"剩余食材池：{_controller.RemainingPoolCount}";

        RebuildCandidateButtons();
        RebuildRewardSection();
        RebuildItemSection();
        RebuildCompanionSection();
        RebuildShopSection();
        RebuildRouteSection();

        bool runOver = _controller.IsRunComplete;
        _skipBowlButton.Disabled = runOver || !_controller.CanSkipBowl;
        // 奖励未选定时 CanAdvanceToNextPot 为 false，「进入下一锅」自动被门控。
        _nextPotButton.Disabled = runOver || !_controller.CanAdvanceToNextPot;
        _endCookingButton.Disabled = runOver || !_controller.CanEndCooking;

        // 重开按钮与其余按钮相反：仅本局结束时可见 / 可点，进行中不可点。
        _restartButton.Visible = runOver;
        _restartButton.Disabled = !runOver;

        RefreshRunEnd(run, pot);

        // 事件驱动自动定位：只有「最需要操作的区块」发生变化才请求滚动一次（下一帧布局完成后执行）。
        var scrollTarget = ResolveScrollTarget();
        if (scrollTarget != _lastScrollTarget)
        {
            _lastScrollTarget = scrollTarget;
            _pendingScrollTarget = scrollTarget;
        }
    }

    /// <summary>
    /// 最近一次饕餮试吃判定行：只读 Run.ChapterBossRecords，无记录时隐藏。
    /// 表现层不做判定，满意度与读数均来自记录。
    /// </summary>
    private void RefreshBossVerdict(RunState run)
    {
        var records = run.ChapterBossRecords;
        if (records.Count == 0)
        {
            _bossVerdictLabel.Visible = false;
            return;
        }

        var last = records[records.Count - 1];
        bool isCurrent = last.Chapter == run.Chapter && last.PotIndex == run.PotIndex;
        string prefix = (last.IsFinalPot ? "最终试吃" : $"第 {last.Chapter} 章末试吃") + (isCurrent ? "" : "（上一次）");
        string verdict = last.Satisfied ? "满意" : "嫌弃";

        string text = $"{prefix}：{verdict}（本锅累计最终分 {last.PotTotalFinalScore} ／ 门槛 {last.Threshold}）";
        if (last.Satisfied)
            text += $"（跨局保留：仙丹粉末 ×{_controller.Meta.ImmortalPowderCount}）";

        _bossVerdictLabel.Text = text;
        _bossVerdictLabel.Visible = true;
    }

    /// <summary>
    /// 本局收尾文案：先判 <see cref="RunState.IsFailed"/>（章末被吞），再读最终锅结算结局。
    /// 两者都没有时隐藏该行。
    /// </summary>
    private void RefreshRunEnd(RunState run, PotState pot)
    {
        if (run.IsFailed)
        {
            _runEndLabel.Text = $"【本局终止】你被饕餮一口吞下，投胎重来。\n{run.FailReason}";
            _runEndLabel.Visible = true;
            return;
        }

        if (run.Outcome != RunOutcome.Unsettled)
        {
            string outcomeText = run.Outcome switch
            {
                RunOutcome.Okay =>
                    "【结局：尚可】饕餮满意地打了个嗝，吐出她吃剩的余味。\n" +
                    $"（跨局保留：仙丹粉末 ×{_controller.Meta.ImmortalPowderCount}）",
                RunOutcome.Restart =>
                    "【结局：重来】饕餮嫌弃了你。",
                _ => "【本局结束】"
            };

            _runEndLabel.Text = outcomeText + "\n天规如此：无论满意与否，投喂者终被吞下，投胎重来。";
            _runEndLabel.Visible = true;
            return;
        }

        _runEndLabel.Visible = false;
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
            btn.CustomMinimumSize = new Vector2(80, 32);
            btn.Disabled = !canSelect;
            btn.Pressed += () => OnCandidatePressed(captured);
            btn.MouseEntered += () => OnCandidateHover(captured);
            btn.MouseExited += HideTooltip;
            _candidateRow.AddChild(btn);
        }
    }

    /// <summary>
    /// 按当前奖励候选重建锅结束奖励区域（最多 X 个）。
    /// 表现层只读 RewardCandidates 并点击回调 GameController，不参与任何流程判断。
    /// </summary>
    private void RebuildRewardSection()
    {
        _rewardSection.Visible = _controller.IsAwaitingReward;

        foreach (Node child in _rewardRow.GetChildren())
        {
            _rewardRow.RemoveChild(child);
            child.QueueFree();
        }

        if (!_controller.IsAwaitingReward)
            return;

        foreach (var candidate in _controller.RewardCandidates)
        {
            var captured = candidate;
            var btn = new Button();
            btn.Text = captured.Definition.Name;
            btn.CustomMinimumSize = new Vector2(80, 32);
            btn.Pressed += () => OnRewardPressed(captured);
            btn.MouseEntered += () => OnRewardHover(captured);
            btn.MouseExited += HideTooltip;
            _rewardRow.AddChild(btn);
        }
    }

    /// <summary>
    /// 按当前伙伴候选 / 已有伙伴重建伙伴区域。
    /// 表现层只读 CompanionCandidates 与 Player.Companions，点击回调 GameController，不参与流程判断。
    /// </summary>
    private void RebuildCompanionSection()
    {
        bool awaiting = _controller.IsAwaitingCompanionChoice;
        _companionSection.Visible = awaiting;

        foreach (Node child in _companionCandidateRow.GetChildren())
        {
            _companionCandidateRow.RemoveChild(child);
            child.QueueFree();
        }

        if (awaiting)
        {
            foreach (var candidate in _controller.CompanionCandidates)
            {
                var captured = candidate;

                var column = new VBoxContainer();
                column.AddThemeConstantOverride("separation", UiMetrics.Gap);

                var info = MakeLabel($"{captured.Name}：{captured.Description}", center: true);
                info.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                info.CustomMinimumSize = new Vector2(240, 0);
                column.AddChild(info);

                var btn = new Button();
                btn.Text = "选择";
                btn.CustomMinimumSize = new Vector2(80, 32);
                btn.Pressed += () => OnCompanionPressed(captured);
                column.AddChild(btn);

                _companionCandidateRow.AddChild(column);
            }
        }

        _skipCompanionButton.Disabled = !awaiting;

        foreach (Node child in _ownedCompanionList.GetChildren())
        {
            _ownedCompanionList.RemoveChild(child);
            child.QueueFree();
        }

        if (_controller.Player.Companions.Count == 0)
        {
            _ownedCompanionList.AddChild(MakeLabel("（暂无）", center: true, minHeight: 24));
        }
        else
        {
            foreach (var companion in _controller.Player.Companions)
            {
                var owned = MakeLabel(
                    $"{companion.Definition.Name}：{companion.Definition.Description}",
                    center: true, minHeight: 24);
                owned.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                _ownedCompanionList.AddChild(owned);
            }
        }
    }

    /// <summary>
    /// 按当前商店报价重建商店区域（仅营业时显示）。
    /// 表现层只读 ShopOffers、调 CanBuy/CanSkipShop 判断，再回调 GameController.Buy/SkipShop，
    /// 不参与任何流程判断。
    /// </summary>
    private void RebuildShopSection()
    {
        bool open = _controller.IsShopOpen;
        _shopSection.Visible = open;

        foreach (Node child in _shopRow.GetChildren())
        {
            _shopRow.RemoveChild(child);
            child.QueueFree();
        }

        if (!open)
            return;

        var offers = _controller.ShopOffers;
        for (int i = 0; i < offers.Count; i++)
        {
            int index = i;
            var offer = offers[i];

            var column = new VBoxContainer();
            column.AddThemeConstantOverride("separation", UiMetrics.Gap);

            column.AddChild(MakeLabel(
                $"{offer.DisplayName}（{offer.Price} 金币）", center: true, minHeight: 24));

            var btn = new Button();
            if (offer.IsPurchased)
            {
                btn.Text = "已购买";
                btn.Disabled = true;
            }
            else
            {
                btn.Text = "购买";
                btn.Disabled = !_controller.CanBuy(index);
            }

            btn.CustomMinimumSize = new Vector2(88, 32);
            btn.Pressed += () => OnBuyPressed(index);
            column.AddChild(btn);

            _shopRow.AddChild(column);
        }

        _skipShopButton.Disabled = !_controller.CanSkipShop;
    }

    /// <summary>
    /// 按当前路线候选重建监味星君区域（仅等待选择时显示）。
    /// 表现层只读 RouteOffers、调 CanChooseRoute/CanSkipRoute 判断，再回调
    /// GameController.ChooseRoute/SkipRoute，不参与任何流程判断。
    /// </summary>
    private void RebuildRouteSection()
    {
        bool awaiting = _controller.IsAwaitingRouteChoice;
        _routeSection.Visible = awaiting;

        foreach (Node child in _routeRow.GetChildren())
        {
            _routeRow.RemoveChild(child);
            child.QueueFree();
        }

        if (!awaiting)
            return;

        foreach (var candidate in _controller.RouteOffers)
        {
            var captured = candidate;

            var column = new VBoxContainer();
            column.AddThemeConstantOverride("separation", UiMetrics.Gap);

            string tag = captured.Kind == RouteKind.FlavorTrend && captured.Theme != null
                ? $"风潮·{ToFlavorName(captured.Theme.Value)}"
                : "保底";
            column.AddChild(MakeLabel($"{captured.Name}（{tag}）", center: true, minHeight: 24));

            var info = MakeLabel(captured.Description, center: true);
            info.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            info.CustomMinimumSize = new Vector2(240, 0);
            column.AddChild(info);

            var btn = new Button();
            btn.Text = "就它";
            btn.CustomMinimumSize = new Vector2(88, 32);
            btn.Disabled = !_controller.CanChooseRoute;
            btn.Pressed += () => OnRoutePressed(captured);
            column.AddChild(btn);

            _routeRow.AddChild(column);
        }

        _skipRouteButton.Disabled = !_controller.CanSkipRoute;
    }

    /// <summary>
    /// 按当前持有道具重建道具区域（每个道具一个按钮）。
    /// 表现层只读 Items、只调 GameController.UseItem，不参与流程判断。
    /// </summary>
    private void RebuildItemSection()
    {
        _itemCountLabel.Text = $"持有道具：{_controller.Items.Count}";

        foreach (Node child in _itemRow.GetChildren())
        {
            _itemRow.RemoveChild(child);
            child.QueueFree();
        }

        bool canUse = _controller.CanUseItem;
        foreach (var item in _controller.Items)
        {
            var captured = item;
            var btn = new Button();
            btn.Text = $"使用：{captured.Definition.Name}";
            btn.CustomMinimumSize = new Vector2(120, 32);
            btn.Disabled = !canUse;
            btn.Pressed += () => OnItemPressed(captured);
            btn.MouseEntered += () => OnItemHover(captured);
            btn.MouseExited += HideTooltip;
            _itemRow.AddChild(btn);
        }
    }

    /// <summary>道具按钮点击：只转发 GameController.UseItem，随后刷新。</summary>
    private void OnItemPressed(ItemInstance item)
    {
        if (!_controller.CanUseItem)
            return;

        _controller.UseItem(item.InstanceId);
        _uiDirty = true;
    }

    private void OnItemHover(ItemInstance item)
    {
        _tooltipLabel.Text = BuildItemTooltip(item.Definition);

        _tooltipAnchor = GetViewport().GetMousePosition() + new Vector2(14, 14);
        _tooltipPanel.Position = _tooltipAnchor;
        _tooltipPanel.Visible = true;
        _tooltipNeedsReposition = true;
    }

    private static string BuildItemTooltip(ItemDefinition def)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"名称：{def.Name}");
        sb.AppendLine($"类型：道具");
        if (def.Effects.Count == 0)
        {
            sb.Append("效果：无");
        }
        else
        {
            foreach (var effect in def.Effects)
                sb.AppendLine(DescribeEffect(effect));
        }
        return sb.ToString().TrimEnd();
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

    /// <summary>
    /// 七味条刷新：每条按「当前值 / 七味最大值」等比映射到 0..12 段，凸显相对分布。
    /// 只读 PotState，不做流程判断；由 RefreshUI（事件驱动置脏）统一调用。
    /// </summary>
    private void RefreshFlavorBars(PotState pot)
    {
        int max = 1;
        foreach (var (_, type) in FlavorRows)
            max = Mathf.Max(max, pot.GetFlavor(type));

        for (int i = 0; i < FlavorRows.Length; i++)
        {
            int value = pot.GetFlavor(FlavorRows[i].Type);
            _flavorBars[i].SetFilled(Mathf.RoundToInt(value / (float)max * FlavorBarSegments));
            _flavorValueLabels[i].Text = value.ToString();
        }
    }

    /// <summary>
    /// 味道熵提示：丰盛倍率 / 寡淡惩罚 / 臭使丰盛失效。只读 PotState 与 ScoreCalculator，不做流程判断。
    /// </summary>
    private static string BuildFlavorEntropyText(PotState pot)
    {
        int types = pot.ActiveFlavorTypeCount;
        if (pot.HasOdor)
            return $"味道种类：{types}（丰盛失效）";
        if (types >= 2)
            return $"味道种类：{types}（丰盛 ×{ScoreCalculator.GetAbundanceMultiplier(pot):0.##}）";
        if (types == 1)
            return $"味道种类：1（寡淡 ×{pot.Config.BlandPenalty:0.##}）";
        return $"味道种类：{types}";
    }

    /// <summary>
    /// 状态行：陈酿 / 固化 / 臭，无状态时返回空串（由调用方隐藏整行）。
    /// </summary>
    private static string BuildFlavorStatusText(PotState pot)
    {
        var parts = new System.Collections.Generic.List<string>();
        if (pot.AgingPool > 0 || pot.AgingAdds > 0)
            parts.Add($"陈酿：{(int)Math.Floor(pot.AgingPool)}（第 {pot.AgingAdds} 次）");
        if (pot.IsSolidified)
            parts.Add("固化：是");
        if (pot.HasOdor)
            parts.Add("【臭】");
        return string.Join("｜", parts);
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
            ("咸", FlavorType.Salty),
            ("麻", FlavorType.Numbing),
        ];
        var parts = System.Array.ConvertAll(flavors, f => $"{f.label}{bottom.GetFlavor(f.type)}");
        return string.Join("   ", parts);
    }

    // ── 食材 Tooltip ──────────────────────────────────────────────────────────

    private static string BuildIngredientTooltip(IngredientDefinition def, IngredientPreview? preview = null)
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

        if (preview is not null)
            sb.Append($"投入后预计分数：{preview.PreviewFinalScore}");

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
        FlavorType.Salty => "咸",
        FlavorType.Numbing => "麻",
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
