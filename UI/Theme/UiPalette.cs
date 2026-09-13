using Godot;

namespace SevenSpices.UI;

public static class UiPalette
{
    // 墨黑底
    public static readonly Color Bg = new("12100d");
    public static readonly Color PanelBg = new("1c1813");
    public static readonly Color PanelBgAlt = new("262019");

    // 青铜绿 / 氧化铜
    public static readonly Color Border = new("4a6b58");
    // 暗金
    public static readonly Color BorderHi = new("c8a24a");
    // 朱砂红（唯一强调色）
    public static readonly Color Accent = new("c8402e");

    // 文本米白
    public static readonly Color Text = new("ede4cf");
    public static readonly Color TextDim = new("9c9078");
    public static readonly Color TextDisabled = new("6b6252");

    // 状态条 / 备用色
    public static readonly Color BarBg = new("2a2119");
    public static readonly Color BarFill = new("b07a45"); // 赭石
    public static readonly Color Azurite = new("4e7c8c"); // 石青

    // 七味标识色（见《界面风格规范》§三，仅用于 HUD 与图标，不落在汤面）
    public static readonly Color FlavorSour = new("a8c545");    // 酸：青柠绿
    public static readonly Color FlavorSweet = new("d9a441");   // 甜：琥珀金
    public static readonly Color FlavorBitter = new("6f6a33");  // 苦：深褐橄榄
    public static readonly Color FlavorSpicy = Accent;          // 辣：朱砂红
    public static readonly Color FlavorUmami = new("efe6cc");   // 鲜：乳白象牙
    public static readonly Color FlavorSalty = new("c9d2d6");   // 咸：月白银白
    public static readonly Color FlavorNumbing = new("8a76b8"); // 麻：青紫罗兰
}
