using Godot;
using SevenSpices.UI;

namespace SevenSpices.UI.Controls;

/// <summary>
/// 纹样饰带：在自身矩形内画一条水平或垂直的青铜回纹 / 雷纹 / 云纹。
/// 纯几何绘制（见 <see cref="PixelMotif"/>），零贴图；改动尺寸相关属性会 QueueRedraw。
/// </summary>
public partial class MotifBand : Control
{
    private MotifKind _kind = MotifKind.Thunder;
    private int _unit = UiMetrics.Unit;
    private int _pen = 1;
    private MotifOrientation _orientation = MotifOrientation.Horizontal;
    private Color _motifColor = UiPalette.Border;

    public MotifKind Kind
    {
        get => _kind;
        set
        {
            _kind = value;
            QueueRedraw();
        }
    }

    /// <summary>笔宽上界：回纹 / 云纹需要 size/4 才能保住内层结构，故统一按 Unit/4 收窄。</summary>
    private int MaxPen => Mathf.Max(1, _unit / 4);

    public int Unit
    {
        get => _unit;
        set
        {
            _unit = Mathf.Max(2, value);
            _pen = Mathf.Clamp(_pen, 1, MaxPen);
            ApplyMinThickness();
            QueueRedraw();
        }
    }

    public int Pen
    {
        get => _pen;
        set
        {
            _pen = Mathf.Clamp(value, 1, MaxPen);
            QueueRedraw();
        }
    }

    public MotifOrientation Orientation
    {
        get => _orientation;
        set
        {
            _orientation = value;
            ApplyMinThickness();
            QueueRedraw();
        }
    }

    public Color MotifColor
    {
        get => _motifColor;
        set
        {
            _motifColor = value;
            QueueRedraw();
        }
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        ApplyMinThickness();
    }

    /// <summary>
    /// 本控件会接管厚度轴（横带的 Y / 竖带的 X）的下界：只把厚度放大到至少 Unit，
    /// 不覆盖调用方在该轴上显式设过的更大厚度。
    /// </summary>
    private void ApplyMinThickness()
    {
        var current = CustomMinimumSize;
        CustomMinimumSize = _orientation == MotifOrientation.Horizontal
            ? new Vector2(current.X, Mathf.Max(current.Y, _unit))
            : new Vector2(Mathf.Max(current.X, _unit), current.Y);
    }

    public override void _Draw()
    {
        PixelMotif.Draw(this, new Rect2(Vector2.Zero, Size), _kind, _unit, _pen, _orientation, _motifColor);
    }
}
