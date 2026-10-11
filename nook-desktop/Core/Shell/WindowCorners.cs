using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace nook_desktop.Core.Shell;

/// <summary>
/// 无边框窗口的圆角。
///
/// 窗口圆角只有两条路:窗口真透明,或交给 DWM 原生裁剪。
/// 前者实测走不通(设了 TransparencyLevelHint 之后 WS_EX_LAYERED 并没设上,
/// 四角会是黑块而不是透出桌面),所以用 DwmSetWindowAttribute 设置
/// DWMWA_WINDOW_CORNER_PREFERENCE,由系统直接裁剪窗口形状。
/// 该属性在最大化/还原后会被系统重置,因此状态变化时要重新应用。
/// </summary>
internal static class WindowCorners
{
    private const int CornerPreferenceAttribute = 33;
    private const int Round = 2;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public static void ApplyRounded(Window window)
    {
        if (window.TryGetPlatformHandle() is not { } handle || handle.Handle == IntPtr.Zero)
        {
            return;
        }

        var preference = Round;
        DwmSetWindowAttribute(handle.Handle, CornerPreferenceAttribute, ref preference, sizeof(int));
    }
}