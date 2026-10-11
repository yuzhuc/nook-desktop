using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using nook_desktop.ViewModels;

namespace nook_desktop.Core.Shell;

/// <summary>
/// 标题栏浮层(键盘 / 话筒 / 耳机)的展开、收起与定位。
///
/// 浮层不用 Flyout:Flyout 是独立的顶层窗口,会画到主窗口外面。
/// 这里把浮层作为窗口内容的最后一层渲染,由本类按触发图标的位置对齐。
///
/// 悬停判定必须用 PointerMoved + 命中测试,不能给宿主挂 PointerEntered/Exited:
/// 这两个事件在 Avalonia 里是 Direct 路由,不冒泡,指针命中的是宿主内部的子元素,
/// 事件永远传不到宿主上,关闭计时器无法被取消。
/// </summary>
internal sealed class TitlePanelController
{
    /// <summary>收起延迟。太长会显得粘,太短会在图标与面板之间那点空隙里误关。</summary>
    private static readonly TimeSpan CloseDelay = TimeSpan.FromMilliseconds(120);

    /// <summary>浮层与图标之间的垂直间距。</summary>
    private const double PanelOffset = 6;

    private readonly Window _window;
    private readonly MainWindowViewModel _viewModel;
    private readonly Control _keyboardAnchor;
    private readonly Control _micAnchor;
    private readonly Control _headphoneAnchor;
    private readonly Control _keyboardHost;
    private readonly Control _micHost;
    private readonly Control _headphoneHost;
    private readonly DispatcherTimer _closeTimer;

    /// <summary>最近一次指针在窗口坐标系里的位置,供 hover 命中测试使用。</summary>
    private Point _lastPointer;

    public TitlePanelController(
        Window window,
        MainWindowViewModel viewModel,
        Control keyboardAnchor,
        Control micAnchor,
        Control headphoneAnchor,
        Control keyboardHost,
        Control micHost,
        Control headphoneHost)
    {
        _window = window;
        _viewModel = viewModel;
        _keyboardAnchor = keyboardAnchor;
        _micAnchor = micAnchor;
        _headphoneAnchor = headphoneAnchor;
        _keyboardHost = keyboardHost;
        _micHost = micHost;
        _headphoneHost = headphoneHost;

        _closeTimer = new DispatcherTimer(DispatcherPriority.Input) { Interval = CloseDelay };
        _closeTimer.Tick += (_, _) =>
        {
            _closeTimer.Stop();
            _viewModel.HidePanels();
        };

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _window.SizeChanged += (_, _) =>
        {
            if (_viewModel.OpenPanel != TitlePanelKind.None)
            {
                Align();
            }
        };
    }

    /// <summary>指针移到某个触发图标上:立刻展开,并取消待执行的收起。</summary>
    public void ShowFor(Control anchor)
    {
        CancelClose();
        _viewModel.ShowPanel(KindOf(anchor));
    }

    /// <summary>指针离开触发图标:重新评估是否应当收起。</summary>
    public void RefreshFromPointer()
    {
        if (!_viewModel.IsAnyPanelOpen)
        {
            return;
        }

        if (IsOverAnyTrigger(_lastPointer) || IsOverOpenPanel(_lastPointer))
        {
            CancelClose();
            return;
        }

        ScheduleClose();
    }

    public void OnPointerMoved(Point position)
    {
        _lastPointer = position;
        RefreshFromPointer();
    }

    /// <summary>指针离开窗口后不会再有 PointerMoved,这里补一次收起,避免浮层残留。</summary>
    public void OnPointerLeftWindow() => ScheduleClose();

    public void Hide()
    {
        CancelClose();
        _viewModel.HidePanels();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.CurrentPage))
        {
            Hide();
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.OpenPanel)
            && _viewModel.OpenPanel != TitlePanelKind.None)
        {
            Align();
        }
    }

    /// <summary>把浮层贴到触发图标下方。图标位置是运行时算的,写死坐标会随布局漂移。</summary>
    private void Align()
    {
        var anchor = AnchorOf(_viewModel.OpenPanel);
        var host = HostOf(_viewModel.OpenPanel);

        if (anchor is null || host is null)
        {
            return;
        }

        if (anchor.TranslatePoint(new Point(0, 0), _window) is not { } origin
            || anchor.TranslatePoint(new Point(0, anchor.Bounds.Height + PanelOffset), _window) is not { } bottom)
        {
            return;
        }

        host.Margin = new Thickness(origin.X, bottom.Y, 0, 0);
    }

    private void ScheduleClose()
    {
        _closeTimer.Stop();
        _closeTimer.Start();
    }

    private void CancelClose() => _closeTimer.Stop();

    private bool IsOverAnyTrigger(Point point)
        => IsOver(_keyboardAnchor, point)
        || IsOver(_micAnchor, point)
        || IsOver(_headphoneAnchor, point);

    private bool IsOverOpenPanel(Point point)
    {
        var host = HostOf(_viewModel.OpenPanel);
        return host is { IsVisible: true } && IsOver(host, point);
    }

    /// <summary>控件在窗口坐标系里的矩形是否包含该点。</summary>
    private bool IsOver(Control control, Point point)
    {
        if (control.Bounds.Width <= 0 || control.Bounds.Height <= 0)
        {
            return false;
        }

        if (control.TranslatePoint(new Point(0, 0), _window) is not { } origin)
        {
            return false;
        }

        return point.X >= origin.X
            && point.X <= origin.X + control.Bounds.Width
            && point.Y >= origin.Y
            && point.Y <= origin.Y + control.Bounds.Height;
    }

    private TitlePanelKind KindOf(Control anchor) =>
        ReferenceEquals(anchor, _micAnchor) ? TitlePanelKind.Mic
        : ReferenceEquals(anchor, _headphoneAnchor) ? TitlePanelKind.Headphone
        : TitlePanelKind.Keyboard;

    private Control? AnchorOf(TitlePanelKind kind) => kind switch
    {
        TitlePanelKind.Keyboard => _keyboardAnchor,
        TitlePanelKind.Mic => _micAnchor,
        TitlePanelKind.Headphone => _headphoneAnchor,
        _ => null,
    };

    private Control? HostOf(TitlePanelKind kind) => kind switch
    {
        TitlePanelKind.Keyboard => _keyboardHost,
        TitlePanelKind.Mic => _micHost,
        TitlePanelKind.Headphone => _headphoneHost,
        _ => null,
    };
}