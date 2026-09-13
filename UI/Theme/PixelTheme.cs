using Godot;

namespace SevenSpices.UI;

public static class PixelTheme
{
    public const string FontResourcePath = "res://UI/Fonts/fusion-pixel-12px-proportional-zh_hans.ttf";

    public static Theme Build()
    {
        var theme = new Theme();

        Font? pixelFont = LoadPixelFont();
        if (pixelFont != null)
        {
            theme.DefaultFont = pixelFont;
        }

        BuildPanels(theme);
        BuildButtons(theme);
        BuildLabels(theme);
        BuildProgressBars(theme);
        BuildScrollBars(theme);

        return theme;
    }

    private static Font? LoadPixelFont()
    {
        if (!ResourceLoader.Exists(FontResourcePath))
        {
            GD.PushWarning($"[PixelTheme] 像素字体资源不存在，回退引擎默认字体：{FontResourcePath}");
            return null;
        }

        var font = ResourceLoader.Load<Font>(FontResourcePath);
        if (font == null)
        {
            GD.PushWarning($"[PixelTheme] 像素字体加载失败，回退引擎默认字体：{FontResourcePath}");
            return null;
        }

        GD.Print($"[PixelTheme] 已加载像素字体：{FontResourcePath}");
        return font;
    }

    private static StyleBoxFlat MakeBox(Color bg, Color border, int borderWidth, int radius, int contentMargin)
    {
        var box = new StyleBoxFlat
        {
            BgColor = bg,
            BorderColor = border,
            AntiAliasing = false,
            BorderBlend = false,
            CornerDetail = 1,
            DrawCenter = true,
        };
        box.SetBorderWidthAll(borderWidth);
        box.SetCornerRadiusAll(radius);
        box.SetContentMarginAll(contentMargin);
        return box;
    }

    private static void BuildPanels(Theme theme)
    {
        theme.SetTypeVariation("PanelPlate", "PanelContainer");
        theme.SetStylebox("panel", "PanelPlate",
            MakeBox(UiPalette.PanelBg, UiPalette.Border, UiMetrics.Border, UiMetrics.Radius, UiMetrics.Pad));

        theme.SetTypeVariation("PanelCard", "PanelContainer");
        theme.SetStylebox("panel", "PanelCard",
            MakeBox(UiPalette.PanelBgAlt, UiPalette.BorderHi, UiMetrics.Border, UiMetrics.RadiusSm, UiMetrics.Pad));

        theme.SetTypeVariation("PanelTooltip", "PanelContainer");
        theme.SetStylebox("panel", "PanelTooltip",
            MakeBox(UiPalette.PanelBgAlt, UiPalette.BorderHi, UiMetrics.Border, UiMetrics.RadiusSm, UiMetrics.Pad));
    }

    private static void BuildButtons(Theme theme)
    {
        theme.SetStylebox("normal", "Button",
            MakeBox(UiPalette.PanelBg, UiPalette.Border, UiMetrics.Border, UiMetrics.RadiusSm, UiMetrics.Pad));
        theme.SetStylebox("hover", "Button",
            MakeBox(UiPalette.PanelBgAlt, UiPalette.BorderHi, UiMetrics.Border, UiMetrics.RadiusSm, UiMetrics.Pad));
        theme.SetStylebox("pressed", "Button",
            MakeBox(UiPalette.PanelBgAlt, UiPalette.Accent, UiMetrics.Border, UiMetrics.RadiusSm, UiMetrics.Pad));
        theme.SetStylebox("disabled", "Button",
            MakeBox(UiPalette.PanelBg, UiPalette.TextDisabled, UiMetrics.Border, UiMetrics.RadiusSm, UiMetrics.Pad));
        theme.SetStylebox("focus", "Button",
            MakeBox(new Color(0, 0, 0, 0), UiPalette.BorderHi, UiMetrics.Border, UiMetrics.RadiusSm, UiMetrics.Pad));

        theme.SetFontSize("font_size", "Button", UiMetrics.FontBody);
        theme.SetColor("font_color", "Button", UiPalette.Text);
        theme.SetColor("font_hover_color", "Button", UiPalette.Text);
        theme.SetColor("font_pressed_color", "Button", UiPalette.Accent);
        theme.SetColor("font_disabled_color", "Button", UiPalette.TextDisabled);
        theme.SetColor("font_focus_color", "Button", UiPalette.Text);
    }

    private static void BuildLabels(Theme theme)
    {
        theme.SetTypeVariation("LabelTitle", "Label");
        theme.SetFontSize("font_size", "LabelTitle", UiMetrics.FontTitle);
        theme.SetColor("font_color", "LabelTitle", UiPalette.Text);

        theme.SetTypeVariation("LabelDim", "Label");
        theme.SetFontSize("font_size", "LabelDim", UiMetrics.FontBody);
        theme.SetColor("font_color", "LabelDim", UiPalette.TextDim);

        theme.SetFontSize("font_size", "Label", UiMetrics.FontBody);
        theme.SetColor("font_color", "Label", UiPalette.Text);
    }

    private static void BuildProgressBars(Theme theme)
    {
        theme.SetStylebox("background", "ProgressBar",
            MakeBox(UiPalette.PanelBg, UiPalette.Border, UiMetrics.Border, UiMetrics.RadiusSm, UiMetrics.Border));

        var fill = MakeBox(UiPalette.BarFill, UiPalette.BorderHi, 0, UiMetrics.RadiusSm, 0);
        fill.SetExpandMarginAll(-UiMetrics.Border);
        theme.SetStylebox("fill", "ProgressBar", fill);

        theme.SetFontSize("font_size", "ProgressBar", UiMetrics.FontBody);
        theme.SetColor("font_color", "ProgressBar", UiPalette.Text);
    }

    private static void BuildScrollBars(Theme theme)
    {
        BuildScrollBar(theme, "VScrollBar");
        BuildScrollBar(theme, "HScrollBar");
    }

    /// <summary>
    /// 滚动条像素皮肤：轨道用底色，滑块用面板色 + 青铜边；悬停/按下换成暗金/朱砂。
    /// 轨道/滑块的内容边距取 Unit/2(4) → StyleBoxFlat 最小尺寸 8px，使滚动条厚度 = 8px（对齐 8px 栅格）；
    /// 滑块无切角（radius=0），避免细条被切掉一半。
    /// </summary>
    private static void BuildScrollBar(Theme theme, string type)
    {
        theme.SetStylebox("scroll", type,
            MakeBox(UiPalette.Bg, UiPalette.Border, 0, 0, UiMetrics.Unit / 2));
        theme.SetStylebox("grabber", type,
            MakeBox(UiPalette.PanelBgAlt, UiPalette.Border, UiMetrics.Border, 0, UiMetrics.Unit / 2));
        theme.SetStylebox("grabber_highlight", type,
            MakeBox(UiPalette.PanelBgAlt, UiPalette.BorderHi, UiMetrics.Border, 0, UiMetrics.Unit / 2));
        theme.SetStylebox("grabber_pressed", type,
            MakeBox(UiPalette.PanelBgAlt, UiPalette.Accent, UiMetrics.Border, 0, UiMetrics.Unit / 2));
    }
}
