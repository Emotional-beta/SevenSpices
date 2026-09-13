namespace SevenSpices.UI;

public static class UiMetrics
{
    // 8px 栅格
    public const int Unit = 8;

    // 设计基准分辨率（窗口下限与整数缩放基准）
    public const int BaseWidth = 640;
    public const int BaseHeight = 360;

    // 边框用偶数，保证整数缩放下为整数物理像素
    public const int Border = 2;

    // 内边距 = 8 的倍数
    public const int Pad = 8;
    public const int PadLg = 16;
    public const int Gap = 8;

    // 切角半径（偶数）
    public const int Radius = 6;
    public const int RadiusSm = 4;

    // 字号必须是像素字体设计尺寸 12 的整数倍
    public const int FontBody = 12;
    public const int FontTitle = 24;
}
