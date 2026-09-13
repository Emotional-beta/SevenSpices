using System.Collections.Generic;
using Godot;

namespace SevenSpices.UI.Controls;

public enum MotifKind
{
    /// <summary>回纹：回字（嵌套方环）。</summary>
    Meander,

    /// <summary>雷纹：方形螺旋（云雷纹的骨架）。</summary>
    Thunder,

    /// <summary>云纹：开口方环（外环开口、内为实心云核）。</summary>
    Cloud,
}

public enum MotifOrientation
{
    Horizontal,
    Vertical,
}

/// <summary>
/// 程序化纹样绘制器：零贴图、零手绘点阵。
/// 所有纹样都由整数像素的横 / 竖线段（DrawRect）在 unit×unit 的单元内生成，
/// 并沿给定方向按 unit 严格周期平铺；供任意 CanvasItem 在 _Draw 中调用。
/// </summary>
public static class PixelMotif
{
    private const int MaxSpiralSegments = 64;

    public static void Draw(
        CanvasItem canvas,
        Rect2 rect,
        MotifKind kind,
        int unit,
        int pen,
        MotifOrientation orientation,
        Color color)
    {
        int x0 = Mathf.RoundToInt(rect.Position.X);
        int y0 = Mathf.RoundToInt(rect.Position.Y);
        int width = Mathf.RoundToInt(rect.Size.X);
        int height = Mathf.RoundToInt(rect.Size.Y);
        if (width <= 0 || height <= 0)
            return;

        int cell = Mathf.Max(1, unit);
        int thickness = orientation == MotifOrientation.Horizontal ? height : width;
        int length = orientation == MotifOrientation.Horizontal ? width : height;

        int cells = length / cell;
        if (cells <= 0)
            return;

        // 单元是正方形：边长取 unit，厚度不足时收缩到厚度，保证不越界。
        int size = Mathf.Min(cell, thickness);
        int stroke = ClampPen(kind, size, pen);
        int crossOffset = (thickness - size) / 2;   // 厚度方向整数居中，无半像素

        var segments = new List<Rect2I>();
        BuildCell(kind, size, stroke, segments);
        if (segments.Count == 0)
            return;

        foreach (Rect2I segment in segments)
        {
            Rect2I mapped = Map(segment, size, orientation);
            int sx = mapped.Position.X;
            int sy = mapped.Position.Y;
            int sw = mapped.Size.X;
            int sh = mapped.Size.Y;
            if (sw <= 0 || sh <= 0)
                continue;

            for (int i = 0; i < cells; i++)
            {
                int ox;
                int oy;
                if (orientation == MotifOrientation.Horizontal)
                {
                    ox = x0 + i * cell;
                    oy = y0 + crossOffset;
                }
                else
                {
                    ox = x0 + crossOffset;
                    oy = y0 + i * cell;
                }

                canvas.DrawRect(new Rect2(ox + sx, oy + sy, sw, sh), color);
            }
        }
    }

    /// <summary>
    /// 按纹样类型夹取笔宽：回纹 / 云纹含嵌套结构，笔宽超过 size/4 会挤掉内层；
    /// 雷纹是单线条螺旋，可放宽到 size/2。直接调用本类时也由此保证结构完整。
    /// </summary>
    private static int ClampPen(MotifKind kind, int size, int pen)
    {
        int max = kind == MotifKind.Thunder ? Mathf.Max(1, size / 2) : Mathf.Max(1, size / 4);
        return Mathf.Clamp(pen, 1, max);
    }

    /// <summary>把单元内的局部矩形按朝向映射到画布：垂直 = 顺时针旋转 90°。</summary>
    private static Rect2I Map(Rect2I r, int size, MotifOrientation orientation)
    {
        if (orientation == MotifOrientation.Horizontal)
            return r;

        return new Rect2I(size - r.Position.Y - r.Size.Y, r.Position.X, r.Size.Y, r.Size.X);
    }

    private static void BuildCell(MotifKind kind, int size, int pen, List<Rect2I> segments)
    {
        switch (kind)
        {
            case MotifKind.Meander:
                AddRing(segments, 0, 0, size, pen, -1, 0);
                AddInner(segments, size, pen);
                break;

            case MotifKind.Thunder:
                BuildSpiral(segments, size, pen);
                break;

            case MotifKind.Cloud:
                // 开口朝上：外环是不闭合的「云头」，内为实心云核。
                AddRing(segments, 0, 0, size, pen, 0, pen * 2);
                AddCore(segments, size, pen);
                break;
        }
    }

    /// <summary>内层方环：外层 inset 2*pen，放不下时退化为实心块。</summary>
    private static void AddInner(List<Rect2I> segments, int size, int pen)
    {
        int inner = size - pen * 4;
        if (inner >= pen * 3)
            AddRing(segments, pen * 2, pen * 2, inner, pen, -1, 0);
        else if (inner > 0)
            AddFilled(segments, pen * 2, pen * 2, inner, inner);
    }

    /// <summary>云核：外环内的实心方块，与回纹的空心内环形成区分。</summary>
    private static void AddCore(List<Rect2I> segments, int size, int pen)
    {
        int core = size - pen * 4;
        if (core > 0)
            AddFilled(segments, pen * 2, pen * 2, core, core);
    }

    /// <summary>
    /// 方形螺旋：从左上角出发，每跑到当前边界就内缩 2*pen 再转向，
    /// 直到框体耗尽。天然由整数横竖线段组成。
    /// </summary>
    private static void BuildSpiral(List<Rect2I> segments, int size, int pen)
    {
        int left = 0;
        int top = 0;
        int right = size - pen;
        int bottom = size - pen;
        int x = left;
        int y = top;
        int heading = 0;    // 0 右 1 下 2 左 3 上

        for (int guard = 0; guard < MaxSpiralSegments; guard++)
        {
            switch (heading)
            {
                case 0:
                    if (right < x)
                        return;
                    AddFilled(segments, x, y, right - x + pen, pen);
                    x = right;
                    top += pen * 2;
                    break;

                case 1:
                    if (bottom < y)
                        return;
                    AddFilled(segments, x, y, pen, bottom - y + pen);
                    y = bottom;
                    right -= pen * 2;
                    break;

                case 2:
                    if (x < left)
                        return;
                    AddFilled(segments, left, y, x - left + pen, pen);
                    x = left;
                    bottom -= pen * 2;
                    break;

                default:
                    if (y < top)
                        return;
                    AddFilled(segments, x, top, pen, y - top + pen);
                    y = top;
                    left += pen * 2;
                    break;
            }

            heading = (heading + 1) % 4;
        }

        GD.PushWarning(
            $"[PixelMotif] 方形螺旋段数达到护栏 {MaxSpiralSegments}（size={size}, pen={pen}），已截断。");
    }

    /// <summary>
    /// 方环：四条边，gapSide 指定的那条边在正中开一个 gapWidth 宽的缺口（-1 表示不开）。
    /// </summary>
    private static void AddRing(List<Rect2I> segments, int x, int y, int size, int pen, int gapSide, int gapWidth)
    {
        if (size <= 0 || pen <= 0)
            return;

        if (size <= pen * 2)
        {
            AddFilled(segments, x, y, size, size);
            return;
        }

        int sideStart = pen;
        int sideLength = size - pen * 2;
        int gap = gapSide < 0 ? 0 : Mathf.Clamp(gapWidth, 0, Mathf.Max(0, sideLength - 2));

        if (gapSide == 0)
            AddSplitX(segments, x, y, size, pen, gap);
        else
            AddFilled(segments, x, y, size, pen);

        if (gapSide == 2)
            AddSplitX(segments, x, y + size - pen, size, pen, gap);
        else
            AddFilled(segments, x, y + size - pen, size, pen);

        if (gapSide == 3)
            AddSplitY(segments, x, y + sideStart, pen, sideLength, gap);
        else
            AddFilled(segments, x, y + sideStart, pen, sideLength);

        if (gapSide == 1)
            AddSplitY(segments, x + size - pen, y + sideStart, pen, sideLength, gap);
        else
            AddFilled(segments, x + size - pen, y + sideStart, pen, sideLength);
    }

    private static void AddSplitX(List<Rect2I> segments, int x, int y, int length, int pen, int gap)
    {
        if (gap <= 0)
        {
            AddFilled(segments, x, y, length, pen);
            return;
        }

        int start = (length - gap) / 2;
        AddFilled(segments, x, y, start, pen);
        AddFilled(segments, x + start + gap, y, length - start - gap, pen);
    }

    private static void AddSplitY(List<Rect2I> segments, int x, int y, int pen, int length, int gap)
    {
        if (gap <= 0)
        {
            AddFilled(segments, x, y, pen, length);
            return;
        }

        int start = (length - gap) / 2;
        AddFilled(segments, x, y, pen, start);
        AddFilled(segments, x, y + start + gap, pen, length - start - gap);
    }

    private static void AddFilled(List<Rect2I> segments, int x, int y, int width, int height)
    {
        if (width <= 0 || height <= 0)
            return;

        segments.Add(new Rect2I(x, y, width, height));
    }
}
