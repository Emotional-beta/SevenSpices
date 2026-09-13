using Godot;
using SevenSpices.UI;
using SevenSpices.UI.Controls;

namespace SevenSpices;

public partial class UiSandbox : Control
{
    private const int WarmupFrames = 12;
    private const string OutputDir = "res://generated-images/ui-m1";

    private PanelContainer _plate = null!;
    private PixelBar _bar = null!;
    private int _frames;
    private bool _done;

    public override void _Ready()
    {
        GetTree().Root.Theme = PixelTheme.Build();
        BuildLayout();
    }

    public override void _Process(double delta)
    {
        if (_done)
        {
            return;
        }

        _frames++;
        if (_frames < WarmupFrames)
        {
            return;
        }

        _done = true;
        bool ok = false;
        try
        {
            ok = CaptureAndVerify();
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[UiSandbox] FAIL：截图/断言过程抛出异常：{ex}");
            ok = false;
        }
        finally
        {
            GetTree().Quit(ok ? 0 : 1);
        }
    }

    private void BuildLayout()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var bg = new ColorRect { Color = UiPalette.Bg };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(bg);

        var outer = new MarginContainer();
        outer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        outer.AddThemeConstantOverride("margin_left", UiMetrics.PadLg);
        outer.AddThemeConstantOverride("margin_right", UiMetrics.PadLg);
        outer.AddThemeConstantOverride("margin_top", UiMetrics.PadLg);
        outer.AddThemeConstantOverride("margin_bottom", UiMetrics.PadLg);
        AddChild(outer);

        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", UiMetrics.Gap);
        outer.AddChild(col);

        col.AddChild(new Label
        {
            Text = "七荤八素 · 像素 UI 沙盒",
            ThemeTypeVariation = "LabelTitle",
        });

        _plate = new PanelContainer { ThemeTypeVariation = "PanelPlate" };
        col.AddChild(_plate);
        var plateMargin = MakeMargin(UiMetrics.Pad);
        _plate.AddChild(plateMargin);
        var plateCol = new VBoxContainer();
        plateCol.AddThemeConstantOverride("separation", UiMetrics.Gap);
        plateMargin.AddChild(plateCol);
        plateCol.AddChild(new Label
        {
            Text = "青铜饕餮纹面板：验证中文正文的像素字体可读性。",
        });
        plateCol.AddChild(new Label
        {
            Text = "次要说明文字（LabelDim），用于对比文本层级。",
            ThemeTypeVariation = "LabelDim",
        });

        var cardRow = new HBoxContainer();
        cardRow.AddThemeConstantOverride("separation", UiMetrics.Gap);
        col.AddChild(cardRow);
        cardRow.AddChild(MakeCard("卡片一", "食材 / 装备"));
        cardRow.AddChild(MakeCard("卡片二", "状态 / 效果"));

        var barRow = new HBoxContainer();
        barRow.AddThemeConstantOverride("separation", UiMetrics.Gap);
        col.AddChild(barRow);
        _bar = new PixelBar { SegmentCount = 7, Filled = 4 };
        _bar.CustomMinimumSize = new Vector2(0, 16);
        _bar.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        barRow.AddChild(_bar);

        var progress = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 100,
            Value = 40,
            ShowPercentage = true,
        };
        progress.CustomMinimumSize = new Vector2(160, 16);
        col.AddChild(progress);

        var buttonRow = new HBoxContainer();
        buttonRow.AddThemeConstantOverride("separation", UiMetrics.Gap);
        col.AddChild(buttonRow);
        buttonRow.AddChild(new Button { Text = "开始烹饪" });
        buttonRow.AddChild(new Button { Text = "翻看食谱" });
        buttonRow.AddChild(new Button { Text = "禁用态", Disabled = true });
    }

    private static MarginContainer MakeMargin(int margin)
    {
        var m = new MarginContainer();
        m.AddThemeConstantOverride("margin_left", margin);
        m.AddThemeConstantOverride("margin_right", margin);
        m.AddThemeConstantOverride("margin_top", margin);
        m.AddThemeConstantOverride("margin_bottom", margin);
        return m;
    }

    private static PanelContainer MakeCard(string title, string body)
    {
        var card = new PanelContainer { ThemeTypeVariation = "PanelCard" };
        card.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var margin = MakeMargin(UiMetrics.Pad);
        card.AddChild(margin);
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", UiMetrics.Gap / 2);
        margin.AddChild(v);
        v.AddChild(new Label { Text = title });
        v.AddChild(new Label { Text = body, ThemeTypeVariation = "LabelDim" });
        return card;
    }

    private bool CaptureAndVerify()
    {
        var texture = GetViewport().GetTexture();
        var image = texture?.GetImage();
        if (image == null)
        {
            GD.PrintErr("[UiSandbox] FAIL：截图失败，GetViewport().GetTexture().GetImage() 返回 null");
            return false;
        }
        if (image.GetFormat() != Image.Format.Rgba8)
        {
            image.Convert(Image.Format.Rgba8);
        }

        Vector2I viewportSize = (Vector2I)GetViewport().GetVisibleRect().Size;
        Vector2I windowSize = GetWindow().Size;

        Transform2D screen = GetViewport().GetScreenTransform();
        Vector2 screenScale = screen.Scale;
        bool transformValid = screenScale.X > 0.5f
            && Mathf.IsEqualApprox(screenScale.X, screenScale.Y)
            && Mathf.IsEqualApprox(screenScale.X, Mathf.Round(screenScale.X));

        int scale;
        string scaleSource;
        if (transformValid)
        {
            scale = Mathf.RoundToInt(screenScale.X);
            scaleSource = "GetScreenTransform";
        }
        else
        {
            // 回退：按整数倍向下取整推算（不做四舍五入）
            int sx = viewportSize.X > 0 ? image.GetWidth() / viewportSize.X : 0;
            int sy = viewportSize.Y > 0 ? image.GetHeight() / viewportSize.Y : 0;
            scale = sx > 0 && sx == sy ? sx : 0;
            scaleSource = "回退(向下取整)";
        }

        GD.Print($"[UiSandbox] 窗口尺寸 = {windowSize.X}x{windowSize.Y}");
        GD.Print($"[UiSandbox] 基准视口尺寸 = {viewportSize.X}x{viewportSize.Y}");
        GD.Print($"[UiSandbox] 截图尺寸 = {image.GetWidth()}x{image.GetHeight()}");
        GD.Print($"[UiSandbox] GetScreenTransform：scale=({screenScale.X},{screenScale.Y}) origin=({screen.Origin.X},{screen.Origin.Y})");
        GD.Print($"[UiSandbox] 整数缩放倍率 = {scale}x（来源：{scaleSource}）");

        bool ok = true;

        // 截图是「视口 × 整数缩放」的渲染目标，不含窗口 letterbox 偏移，
        // 因此图像坐标 = 画布坐标 × scale（偏移恒为 0）。
        bool imageMatchesScale = scale > 0
            && image.GetWidth() == viewportSize.X * scale
            && image.GetHeight() == viewportSize.Y * scale;
        bool windowMatchesScale = scale > 0
            && viewportSize.X > 0
            && viewportSize.Y > 0
            && windowSize.X == viewportSize.X * scale
            && windowSize.Y == viewportSize.Y * scale;

        if (!imageMatchesScale)
        {
            GD.PrintErr(
                $"[UiSandbox] FAIL：截图 {image.GetWidth()}x{image.GetHeight()} 与「视口 {viewportSize.X}x{viewportSize.Y} × {scale}」不一致，无法确定整数缩放。");
            ok = false;
        }

        if (!windowMatchesScale)
        {
            GD.PrintErr(
                $"[UiSandbox] FAIL：窗口 {windowSize.X}x{windowSize.Y} 不是基准视口 {viewportSize.X}x{viewportSize.Y} 的整数倍"
                + $"（当前有效缩放 {scale}x）。请把窗口设为 640x360 的整数倍（如 1280x720）。");
            ok = false;
        }

        const int offX = 0;
        const int offY = 0;

        string dir = ProjectSettings.GlobalizePath(OutputDir);
        Error mkdir = DirAccess.MakeDirRecursiveAbsolute(dir);
        if (mkdir != Error.Ok && mkdir != Error.AlreadyExists)
        {
            GD.PrintErr($"[UiSandbox] FAIL：无法创建输出目录 {dir}，错误 = {mkdir}");
            return false;
        }

        string fullPath = dir.PathJoin("verify.png");
        Error saveErr = image.SavePng(fullPath);
        GD.Print($"[UiSandbox] verify.png -> {fullPath}（保存结果 = {saveErr}）");
        if (saveErr != Error.Ok)
        {
            GD.PrintErr($"[UiSandbox] FAIL：verify.png 写盘失败，错误 = {saveErr}");
            ok = false;
        }

        string zoomPath = SaveCornerZoom(image, dir, offX, offY, scale, out Error zoomErr);
        GD.Print($"[UiSandbox] verify-corner-zoom.png -> {zoomPath}（保存结果 = {zoomErr}）");
        if (zoomErr != Error.Ok)
        {
            GD.PrintErr($"[UiSandbox] FAIL：verify-corner-zoom.png 写盘失败，错误 = {zoomErr}");
            ok = false;
        }

        if (imageMatchesScale && windowMatchesScale)
        {
            ok &= RunAssertions(image, offX, offY, scale);
        }
        else
        {
            GD.Print("[UiSandbox] 程序化断言：SKIP（窗口非基准整数倍，像素校验不适用）");
        }

        GD.Print($"[UiSandbox] 总判定：{(ok ? "PASS" : "FAIL")}");
        return ok;
    }

    private string SaveCornerZoom(Image image, string dir, int offX, int offY, int scale, out Error saveError)
    {
        Rect2 plateRect = _plate.GetGlobalRect();
        int cornerX = offX + Mathf.RoundToInt(plateRect.Position.X) * scale;
        int cornerY = offY + Mathf.RoundToInt(plateRect.Position.Y) * scale;
        int side = 12 * scale;

        var region = new Rect2I(cornerX - 4, cornerY - 4, side, side);
        region = region.Intersection(new Rect2I(0, 0, image.GetWidth(), image.GetHeight()));

        var crop = image.GetRegion(region);
        crop.Resize(crop.GetWidth() * 8, crop.GetHeight() * 8, Image.Interpolation.Nearest);

        string path = dir.PathJoin("verify-corner-zoom.png");
        saveError = crop.SavePng(path);
        return path;
    }

    private bool RunAssertions(Image image, int offX, int offY, int scale)
    {
        Rect2 plateRect = _plate.GetGlobalRect();
        int px = Mathf.RoundToInt(plateRect.Position.X);
        int py = Mathf.RoundToInt(plateRect.Position.Y);
        int pw = Mathf.RoundToInt(plateRect.Size.X);

        int ph = Mathf.RoundToInt(plateRect.Size.Y);
        int scanCanvasY = py + ph / 2;
        int scanY = offY + scanCanvasY * scale;
        int expectedBand = UiMetrics.Border * scale;

        GD.Print($"[UiSandbox] 面板矩形(画布) = x:{px} y:{py} w:{pw} h:{ph}，扫描行 y(画布) = {scanCanvasY} -> y(物理) = {scanY}");
        GD.Print($"[UiSandbox] 期望边框色带宽度 = Border({UiMetrics.Border}) x 缩放({scale}) = {expectedBand}px");

        bool pass = true;
        pass &= CheckBorderBand(image, offX + px * scale, scanY, expectedBand, "左边框");
        pass &= CheckBorderBand(image, offX + (px + pw - UiMetrics.Border) * scale, scanY, expectedBand, "右边框");

        GD.Print($"[UiSandbox] 程序化断言：{(pass ? "PASS" : "FAIL")}");
        return pass;
    }

    private static bool CheckBorderBand(Image image, int xStart, int y, int expectedWidth, string name)
    {
        Color border = UiPalette.Border;
        int width = image.GetWidth();
        if (xStart <= 0 || xStart >= width || y < 0 || y >= image.GetHeight())
        {
            GD.PrintErr($"[UiSandbox] {name} FAIL：扫描点越界 ({xStart},{y})，图像 {width}x{image.GetHeight()}");
            return false;
        }

        int lo = xStart;
        while (lo - 1 >= 0 && ColorEq(image.GetPixel(lo - 1, y), border))
        {
            lo--;
        }
        int hi = xStart;
        while (hi + 1 < width && ColorEq(image.GetPixel(hi + 1, y), border))
        {
            hi++;
        }
        int actualWidth = hi - lo + 1;

        bool ok = true;
        if (actualWidth != expectedWidth)
        {
            ok = false;
            GD.PrintErr($"[UiSandbox] {name} 带宽 FAIL：实测 {actualWidth}px vs 期望 {expectedWidth}px");
        }
        else
        {
            GD.Print($"[UiSandbox] {name} 带宽 PASS：实测 {actualWidth}px == 期望 {expectedWidth}px");
        }

        for (int x = lo; x <= hi; x++)
        {
            Color c = image.GetPixel(x, y);
            if (!ColorEq(c, border))
            {
                ok = false;
                GD.PrintErr($"[UiSandbox] {name} FAIL：色带内像素 ({x},{y}) = {c.ToHtml()} 非边框色 {border.ToHtml()}");
                break;
            }
        }

        Color outside = lo - 1 >= 0 ? image.GetPixel(lo - 1, y) : Colors.Magenta;
        Color inside = hi + 1 < width ? image.GetPixel(hi + 1, y) : Colors.Magenta;

        if (!IsPaletteColor(outside))
        {
            ok = false;
            GD.PrintErr($"[UiSandbox] {name} FAIL：外侧像素 {outside.ToHtml()} 不在调色板（疑似抗锯齿混合）");
        }
        else
        {
            GD.Print($"[UiSandbox] {name} 外侧像素 = {outside.ToHtml()}（调色板内，无混合）");
        }

        if (!IsPaletteColor(inside))
        {
            ok = false;
            GD.PrintErr($"[UiSandbox] {name} FAIL：内侧像素 {inside.ToHtml()} 不在调色板（疑似抗锯齿混合）");
        }
        else
        {
            GD.Print($"[UiSandbox] {name} 内侧像素 = {inside.ToHtml()}（调色板内，无混合）");
        }

        return ok;
    }

    private static bool ColorEq(Color a, Color b)
    {
        const float tol = 1.0f / 255.0f + 0.0001f;
        return Mathf.Abs(a.R - b.R) <= tol
            && Mathf.Abs(a.G - b.G) <= tol
            && Mathf.Abs(a.B - b.B) <= tol
            && Mathf.Abs(a.A - b.A) <= tol;
    }

    private static bool IsPaletteColor(Color c)
    {
        return ColorEq(c, UiPalette.Bg)
            || ColorEq(c, UiPalette.PanelBg)
            || ColorEq(c, UiPalette.PanelBgAlt)
            || ColorEq(c, UiPalette.Border)
            || ColorEq(c, UiPalette.BorderHi)
            || ColorEq(c, UiPalette.Accent)
            || ColorEq(c, UiPalette.Text)
            || ColorEq(c, UiPalette.TextDim)
            || ColorEq(c, UiPalette.TextDisabled)
            || ColorEq(c, UiPalette.BarBg)
            || ColorEq(c, UiPalette.BarFill)
            || ColorEq(c, UiPalette.Azurite);
    }
}
