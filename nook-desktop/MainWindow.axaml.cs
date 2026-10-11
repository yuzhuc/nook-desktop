using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using nook_desktop.Core.Shell;
using nook_desktop.Services;
using nook_desktop.ViewModels;

namespace nook_desktop;

public partial class MainWindow : Window, IWindowService
{
    private WindowEdgeResizer? _resizer;
    private TitlePanelController? _panels;
    private MainWindowViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        // 缩放命中与定位都必须走隧道阶段:冒泡阶段的事件会被子控件标记为已处理,
        // 四边常压在群组卡片这类 Button 上,走了冒泡缩放就会失灵。
        AddHandler(PointerPressedEvent, OnWindowPointerPressed, RoutingStrategies.Tunnel);
        AddHandler(PointerMovedEvent, OnWindowPointerMoved, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, OnWindowPointerReleased, RoutingStrategies.Tunnel);
        AddHandler(PointerExitedEvent, OnWindowPointerExited);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        // 外壳组件与窗口同生命周期,只在首次拿到 DataContext 时装配一次。
        if (ReferenceEquals(_viewModel, viewModel))
        {
            return;
        }

        _viewModel = viewModel;
        viewModel.Window = this;

        _resizer = new WindowEdgeResizer(this);
        _panels = new TitlePanelController(
            this,
            viewModel,
            KeyboardButton,
            MicButton,
            HeadphoneButton,
            KeyboardPanelHost,
            MicPanelHost,
            HeadphonePanelHost);
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        WindowCorners.ApplyRounded(this);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == WindowStateProperty)
        {
            WindowCorners.ApplyRounded(this);
        }
    }

    // ---------------------------------------------------------------- 输入分发

    private void OnWindowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_resizer?.TryBegin(e) == true)
        {
            e.Handled = true;
        }
    }

    private void OnWindowPointerMoved(object? sender, PointerEventArgs e)
    {
        var position = e.GetPosition(this);

        if (_resizer is { IsResizing: true })
        {
            _resizer.Move(e);
            e.Handled = true;
            return;
        }

        _resizer?.UpdateCursor(position);
        _panels?.OnPointerMoved(position);
    }

    private void OnWindowPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _resizer?.End(e.Pointer);
    }

    private void OnWindowPointerExited(object? sender, PointerEventArgs e)
    {
        if (_resizer is { IsResizing: true })
        {
            return;
        }

        _resizer?.ResetCursor();
        _panels?.OnPointerLeftWindow();
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _resizer?.End(null);
    }

    // ---------------------------------------------------------------- 标题栏

    /// <summary>标题栏空白区域按下左键拖动窗口。落点在缩放带上时交给缩放逻辑。</summary>
    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || _resizer?.IsOnEdge(e.GetPosition(this)) == true)
        {
            return;
        }

        _panels?.Hide();
        BeginMoveDrag(e);
        e.Handled = true;
    }

    /// <summary>点击浮层之外的内容区收起浮层。</summary>
    private void OnOverlayScrimPressed(object? sender, PointerPressedEventArgs e)
    {
        _panels?.Hide();
        e.Handled = true;
    }

    private void OnDeviceIconPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is Control anchor)
        {
            _panels?.ShowFor(anchor);
        }
    }

    private void OnDeviceIconPointerExited(object? sender, PointerEventArgs e) => _panels?.RefreshFromPointer();

    // ---------------------------------------------------------------- 窗口服务

    public void Minimize() => WindowState = WindowState.Minimized;

    public void ToggleMaximize() =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
}