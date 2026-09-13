using Godot;

namespace SevenSpices.UI.Controls;

public partial class PixelBar : Control
{
    private int _segmentCount = 7;
    private int _filled = 0;
    private int _segmentGap = 4;
    private int _borderWidth = UiMetrics.Border;

    public int SegmentCount
    {
        get => _segmentCount;
        set
        {
            _segmentCount = Mathf.Max(1, value);
            _filled = Mathf.Clamp(_filled, 0, _segmentCount);
            QueueRedraw();
        }
    }

    public int Filled
    {
        get => _filled;
        set
        {
            _filled = Mathf.Clamp(value, 0, _segmentCount);
            QueueRedraw();
        }
    }

    public int SegmentGap
    {
        get => _segmentGap;
        set
        {
            _segmentGap = Mathf.Max(0, value);
            QueueRedraw();
        }
    }

    public int BorderWidth
    {
        get => _borderWidth;
        set
        {
            _borderWidth = Mathf.Max(0, value);
            QueueRedraw();
        }
    }

    private Color _backgroundColor = UiPalette.BarBg;
    private Color _fillColor = UiPalette.BarFill;
    private Color _borderColor = UiPalette.Border;

    public Color BackgroundColor
    {
        get => _backgroundColor;
        set
        {
            _backgroundColor = value;
            QueueRedraw();
        }
    }

    public Color FillColor
    {
        get => _fillColor;
        set
        {
            _fillColor = value;
            QueueRedraw();
        }
    }

    public Color BorderColor
    {
        get => _borderColor;
        set
        {
            _borderColor = value;
            QueueRedraw();
        }
    }

    public void SetFilled(int value) => Filled = value;

    public override void _Draw()
    {
        int totalWidth = Mathf.RoundToInt(Size.X);
        int height = Mathf.RoundToInt(Size.Y);
        if (totalWidth <= 0 || height <= 0)
        {
            return;
        }

        int n = _segmentCount;

        // 按设定间隙分配；可用宽度不足以给每段至少 1px 时收窄间隙，再退化为无间隙。
        // 绝不产生负段宽或异常宽的末段。
        int gap = Mathf.Max(0, _segmentGap);
        int avail = totalWidth - gap * (n - 1);
        if (avail < n)
        {
            gap = 0;
            avail = totalWidth;
        }
        if (avail < n)
        {
            return;     // 宽度连每段 1px 都放不下
        }

        int segmentWidth = avail / n;
        // 余数均匀分给前几段，避免旧实现里末段独吞余数而显得异常宽。
        int remainder = avail - segmentWidth * n;

        // 边框在纵向放得下时，段内按「边框 + 填充」绘制；否则整段直接填色。
        bool borderFits = _borderWidth > 0 && height > _borderWidth * 2;

        int x = 0;
        for (int i = 0; i < n; i++)
        {
            if (x >= totalWidth)
            {
                break;
            }

            int width = segmentWidth + (i < remainder ? 1 : 0);
            width = Mathf.Min(width, totalWidth - x);

            var segment = new Rect2(x, 0, width, height);
            if (borderFits && width > _borderWidth * 2)
            {
                DrawRect(segment, BorderColor);
                var inner = new Rect2(
                    x + _borderWidth,
                    _borderWidth,
                    width - _borderWidth * 2,
                    height - _borderWidth * 2);
                DrawRect(inner, i < _filled ? FillColor : BackgroundColor);
            }
            else
            {
                // 边框挤不下（或 BorderWidth=0）：整段按填充/底色绘制，保证 Filled 真实可见。
                DrawRect(segment, i < _filled ? FillColor : BackgroundColor);
            }

            x += width + gap;
        }
    }
}
