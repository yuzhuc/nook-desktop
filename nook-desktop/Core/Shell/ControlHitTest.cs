using Avalonia;
using Avalonia.Controls;

namespace nook_desktop.Core.Shell;

/// <summary>控件在窗口坐标系里的矩形命中测试。标题栏浮层和气泡高亮都用它。</summary>
internal static class ControlHitTest
{
    public static bool IsOver(Control control, Visual root, Point point)
    {
        if (control.Bounds.Width <= 0 || control.Bounds.Height <= 0)
        {
            return false;
        }

        if (control.TranslatePoint(new Point(0, 0), root) is not { } origin)
        {
            return false;
        }

        return point.X >= origin.X
            && point.X <= origin.X + control.Bounds.Width
            && point.Y >= origin.Y
            && point.Y <= origin.Y + control.Bounds.Height;
    }
}
