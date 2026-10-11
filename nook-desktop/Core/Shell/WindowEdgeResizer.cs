using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace nook_desktop.Core.Shell;

/// <summary>
/// 无边框窗口的边缘缩放。
///
/// WindowDecorations=None 会连同 WS_THICKFRAME 一起去掉,BeginResizeDrag 随之失效,
/// 所以这里在窗口层面自行命中四边六像素带,按下后捕获指针,手动驱动位置与尺寸。
/// 命中与缩放都走隧道阶段:边缘常常压在群组卡片这类 Button 上,
/// 冒泡阶段的事件会被子控件标记为已处理,缩放会彻底失灵。
/// </summary>
internal sealed class WindowEdgeResizer
{
    /// <summary>缩放命中厚度。无系统边框时,这几条带是唯一的拖拽入口。</summary>
    private const double HitThickness = 6;

    private static readonly Cursor CursorTop = new(StandardCursorType.TopSide);
    private static readonly Cursor CursorBottom = new(StandardCursorType.BottomSide);
    private static readonly Cursor CursorLeft = new(StandardCursorType.LeftSide);
    private static readonly Cursor CursorRight = new(StandardCursorType.RightSide);
    private static readonly Cursor CursorTopLeft = new(StandardCursorType.TopLeftCorner);
    private static readonly Cursor CursorTopRight = new(StandardCursorType.TopRightCorner);
    private static readonly Cursor CursorBottomLeft = new(StandardCursorType.BottomLeftCorner);
    private static readonly Cursor CursorBottomRight = new(StandardCursorType.BottomRightCorner);

    private readonly Window _window;

    private WindowEdge _edge;
    private PixelPoint _startScreen;
    private double _startLeft;
    private double _startTop;
    private double _startWidth;
    private double _startHeight;

    /// <summary>当前写入窗口的光标。记住它是为了只在真正变化时才赋值,避免每帧重复写入引起抖动。</summary>
    private Cursor _cursor = Cursor.Default;

    public WindowEdgeResizer(Window window) => _window = window;

    public bool IsResizing { get; private set; }

    /// <summary>坐标是否落在某条缩放带上。供标题栏拖拽区分"拖窗口"与"改尺寸"。</summary>
    public bool IsOnEdge(Point point) => TryResolveEdge(point, out _);

    /// <summary>尝试开始一次缩放。命中边缘并成功接管返回 true。</summary>
    public bool TryBegin(PointerPressedEventArgs e)
    {
        if (IsResizing || !e.GetCurrentPoint(_window).Properties.IsLeftButtonPressed)
        {
            return false;
        }

        if (!TryResolveEdge(e.GetPosition(_window), out var edge))
        {
            return false;
        }

        if (TopLevel.GetTopLevel(_window) is not { } top)
        {
            return false;
        }

        _edge = edge;
        _startScreen = top.PointToScreen(e.GetPosition(top));
        _startLeft = _window.Position.X;
        _startTop = _window.Position.Y;
        _startWidth = _window.Width;
        _startHeight = _window.Height;
        IsResizing = true;

        // 捕获到窗口自身,指针移出窗口范围也能继续收到移动事件。
        e.Pointer.Capture(_window);
        return true;
    }

    public void Move(PointerEventArgs e)
    {
        if (!IsResizing || TopLevel.GetTopLevel(_window) is not { } top)
        {
            return;
        }

        var current = top.PointToScreen(e.GetPosition(top));
        Apply(current.X - _startScreen.X, current.Y - _startScreen.Y);
    }

    public void End(IPointer? pointer)
    {
        if (!IsResizing)
        {
            return;
        }

        IsResizing = false;
        pointer?.Capture(null);
        ResetCursor();
    }

    /// <summary>指针离开窗口时还原光标,否则会隔着别的窗口残留缩放箭头。</summary>
    public void ResetCursor() => SetCursor(Cursor.Default);

    /// <summary>按指针位置切换边缘光标。</summary>
    public void UpdateCursor(Point point)
    {
        var next = TryResolveEdge(point, out var edge)
            ? edge switch
            {
                WindowEdge.North => CursorTop,
                WindowEdge.South => CursorBottom,
                WindowEdge.West => CursorLeft,
                WindowEdge.East => CursorRight,
                WindowEdge.NorthWest => CursorTopLeft,
                WindowEdge.NorthEast => CursorTopRight,
                WindowEdge.SouthWest => CursorBottomLeft,
                WindowEdge.SouthEast => CursorBottomRight,
                _ => Cursor.Default,
            }
            : Cursor.Default;

        SetCursor(next);
    }

    private void SetCursor(Cursor next)
    {
        if (ReferenceEquals(next, _cursor))
        {
            return;
        }

        _cursor = next;
        _window.Cursor = next;
    }

    /// <summary>判断坐标落在哪条缩放带上,落在角上时返回对角方向。</summary>
    private bool TryResolveEdge(Point point, out WindowEdge edge)
    {
        edge = default;

        // 最大化时窗口铺满工作区,没有真正的边缘可拖。
        if (_window.WindowState != WindowState.Normal)
        {
            return false;
        }

        var width = _window.Bounds.Width;
        var height = _window.Bounds.Height;
        var nearLeft = point.X <= HitThickness;
        var nearRight = point.X >= width - HitThickness;
        var nearTop = point.Y <= HitThickness;
        var nearBottom = point.Y >= height - HitThickness;

        // WindowEdge.NorthWest 的底层值恰好是 0(等于 default),
        // 所以命中与否一律由这里的显式返回决定,不能反推。
        switch (nearLeft, nearRight, nearTop, nearBottom)
        {
            case (true, false, true, false):
                edge = WindowEdge.NorthWest;
                return true;
            case (false, true, true, false):
                edge = WindowEdge.NorthEast;
                return true;
            case (true, false, false, true):
                edge = WindowEdge.SouthWest;
                return true;
            case (false, true, false, true):
                edge = WindowEdge.SouthEast;
                return true;
            case (true, false, false, false):
                edge = WindowEdge.West;
                return true;
            case (false, true, false, false):
                edge = WindowEdge.East;
                return true;
            case (false, false, true, false):
                edge = WindowEdge.North;
                return true;
            case (false, false, false, true):
                edge = WindowEdge.South;
                return true;
            default:
                return false;
        }
    }

    /// <summary>按边缘方向应用拖拽位移。</summary>
    private void Apply(double dx, double dy)
    {
        var left = _startLeft;
        var top = _startTop;
        var width = _startWidth;
        var height = _startHeight;

        if (_edge is WindowEdge.West or WindowEdge.NorthWest or WindowEdge.SouthWest)
        {
            left += dx;
            width -= dx;
        }

        if (_edge is WindowEdge.East or WindowEdge.NorthEast or WindowEdge.SouthEast)
        {
            width += dx;
        }

        if (_edge is WindowEdge.North or WindowEdge.NorthWest or WindowEdge.NorthEast)
        {
            top += dy;
            height -= dy;
        }

        if (_edge is WindowEdge.South or WindowEdge.SouthWest or WindowEdge.SouthEast)
        {
            height += dy;
        }

        // 达到最小尺寸:动的是哪条边,就把那条边推回去,避免窗口跑偏。
        if (width < _window.MinWidth)
        {
            width = _window.MinWidth;
            if (_edge is WindowEdge.West or WindowEdge.NorthWest or WindowEdge.SouthWest)
            {
                left = _startLeft + _startWidth - _window.MinWidth;
            }
        }

        if (height < _window.MinHeight)
        {
            height = _window.MinHeight;
            if (_edge is WindowEdge.North or WindowEdge.NorthWest or WindowEdge.NorthEast)
            {
                top = _startTop + _startHeight - _window.MinHeight;
            }
        }

        _window.Position = new PixelPoint((int)Math.Round(left), (int)Math.Round(top));
        _window.Width = width;
        _window.Height = height;
    }
}