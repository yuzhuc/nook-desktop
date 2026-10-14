using System;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using nook_desktop.ViewModels.Room;

namespace nook_desktop.Views.Room;

public partial class RoomView : UserControl
{
    private RoomViewModel? _viewModel;


    /// <summary>这一次进来是否已经把高亮块摆好;没摆好时每次布局重算,摆好就停。</summary>
    private bool _sliderPlaced;

    /// <summary>群组设置菜单摆好没有(没摆好时每次布局重算,摆好就停)。</summary>
    private bool _menuAligned;

    /// <summary>频道右键菜单的弹出位置(右键点在页面里的坐标)和它摆好没有。</summary>
    private Point _channelMenuOrigin;
    private bool _channelMenuAligned;

    public RoomView()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;
        LayoutUpdated += OnLayoutUpdated;

        // 多行输入框的类处理器会先把回车吃掉,所以改在隧道阶段拦;展开态的回车留给换行
        MessageInput.AddHandler(
            Avalonia.Input.InputElement.KeyDownEvent,
            OnMessageInputKeyDown,
            Avalonia.Interactivity.RoutingStrategies.Tunnel);
        PointerPressed += OnRootPointerPressed;

        _panelHideTimer.Tick += (_, _) =>
        {
            _panelHideTimer.Stop();
            HidePanels();
        };
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as RoomViewModel;

        if (_viewModel is null)
        {
            return;
        }

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        // 换群组等于换了整页:高亮块要重新摆,布局没跑完之前一直重试
        _sliderPlaced = false;
        Dispatcher.UIThread.Post(() => UpdateSlider(animate: false), DispatcherPriority.Loaded);
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (!_sliderPlaced)
        {
            UpdateSlider(animate: false);
        }

        if (!_menuAligned)
        {
            AlignGroupSettingsMenu();
        }

        if (!_channelMenuAligned)
        {
            AlignChannelMenu();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RoomViewModel.SelectedChannel))
        {
            Dispatcher.UIThread.Post(() => UpdateSlider(animate: true), DispatcherPriority.Loaded);
        }

        if (e.PropertyName == nameof(RoomViewModel.IsGroupSettingsMenuOpen))
        {
            _menuAligned = false;
        }
    }

    /// <summary>把高亮块挪到选中频道那一行。选中就滑动过去,只有进页面第一次摆位不做动画。</summary>
    private void UpdateSlider(bool animate)
    {
        if (_viewModel?.SelectedChannel is not { } channel)
        {
            ChannelSlider.IsVisible = false;
            return;
        }

        if (FindRow(channel) is not { } row
            || row.TranslatePoint(new Point(0, 0), ChannelListLayer) is not { } origin
            || ChannelSlider.RenderTransform is not TranslateTransform offset)
        {
            ChannelSlider.IsVisible = false;
            return;
        }

        ChannelSlider.IsVisible = true;
        _sliderPlaced = true;

        if (animate)
        {
            offset.Y = origin.Y;
            return;
        }

        // 进页面第一次摆位不滑:临时摘掉过渡直接落位
        var transitions = offset.Transitions;
        offset.Transitions = null;
        offset.Y = origin.Y;
        offset.Transitions = transitions;
    }

    /// <summary>左键点菜单外面就把开着的菜单收起来;点菜单自己或那个 ⋮ 按钮不收。</summary>
    private void OnRootPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (e.Source is not Visual source)
        {
            return;
        }

        // 群组设置菜单:点任何地方(含右键)都收,只留它自己和那个 ⋮ 不收

        if (_viewModel is { IsGroupSettingsMenuOpen: true }
            && !IsInside(source, GroupSettingsMenu)
            && !IsInside(source, GroupSettingsButton))
        {
            _viewModel.CloseGroupSettingsMenuCommand.Execute(null);
        }

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed
            && _viewModel is { IsChannelMenuOpen: true }
            && !IsInside(source, ChannelMenu))
        {
            _viewModel.CloseChannelMenuCommand.Execute(null);

        }

        if (_viewModel is { IsMemberSearchOpen: true }
            && !IsInside(source, MemberSearchBox)
            && !IsInside(source, MemberSearchButton))
        {
            _viewModel.CloseMemberSearchCommand.Execute(null);
        }
    }

    /// <summary>成员栏那个放大镜:点了展开搜索框(已经开着就只把光标放进去),照 Oopz。</summary>
    private void OnMemberSearchClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_viewModel is { IsMemberSearchOpen: false })
        {
            _viewModel.OpenMemberSearchCommand.Execute(null);
        }

        MemberSearchBox.Focus();
    }

    /// <summary>频道右键:记住是哪个频道、右键点在哪,菜单按这两样显示。</summary>
    private void OnChannelRightPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (_viewModel is null
            || sender is not Control row
            || !e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
        {
            return;
        }

        // 主页那一行的 DataContext 是页面本身(它绑 Home.*),所以取不到频道时按主页算
        var channel = row.DataContext as RoomChannelViewModel ?? _viewModel.Home;

        _channelMenuOrigin = e.GetPosition(this);
        _channelMenuAligned = false;
        _viewModel.OpenChannelMenu(channel);
        e.Handled = true;
    }

    /// <summary>频道菜单贴着右键点弹,并夹在窗口内。</summary>
    private void AlignChannelMenu()
    {
        if (_viewModel is not { IsChannelMenuOpen: true } || ChannelMenu.Bounds.Height <= 0)
        {
            _channelMenuAligned = false;
            return;
        }

        var x = Math.Min(_channelMenuOrigin.X, Math.Max(0, Bounds.Width - ChannelMenu.Bounds.Width - 8));
        var y = Math.Min(_channelMenuOrigin.Y, Math.Max(0, Bounds.Height - ChannelMenu.Bounds.Height - 8));
        ChannelMenu.Margin = new Thickness(x, y, 0, 0);
        _channelMenuAligned = true;
    }

    private static bool IsInside(Visual source, Visual target)
    {
        for (Visual? node = source; node is not null; node = node.GetVisualParent())
        {
            if (ReferenceEquals(node, target))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>把群组设置菜单贴在 ⋮ 按钮右下角,并夹在窗口内(Oopz 也是往右下展开)。</summary>
    private void AlignGroupSettingsMenu()
    {
        if (_viewModel is not { IsGroupSettingsMenuOpen: true } || GroupSettingsMenu.Bounds.Height <= 0)
        {
            _menuAligned = false;
            return;
        }

        if (GroupSettingsButton.TranslatePoint(new Point(0, 0), this) is not { } origin)
        {
            _menuAligned = false;
            return;
        }

        var x = Math.Min(origin.X + 13, Math.Max(0, Bounds.Width - GroupSettingsMenu.Bounds.Width - 8));
        var y = Math.Min(origin.Y + 8, Math.Max(0, Bounds.Height - GroupSettingsMenu.Bounds.Height - 8));
        GroupSettingsMenu.Margin = new Thickness(x, y, 0, 0);
        _menuAligned = true;
    }

    /// <summary>
    /// 分组是嵌套 ItemsControl,拿不到稳定索引,所以按 DataContext 找行。
    /// 主页那一行不在列表里,它的 DataContext 是页面自己,只能按名字取。
    /// </summary>
    private Control? FindRow(RoomChannelViewModel channel) => ReferenceEquals(channel, _viewModel?.Home)
        ? HomeChannelRow
        : this.GetVisualDescendants()
            .OfType<Button>()
            .FirstOrDefault(control => ReferenceEquals(control.DataContext, channel));
    /// <summary>输入框回车发送(Oopz 的输入条没有发送按钮,回车就是发送)。</summary>
    private void OnMessageInputKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key != Avalonia.Input.Key.Enter
            || sender is not TextBox input
            || DataContext is not RoomViewModel viewModel
            || viewModel.IsComposerExpanded)
        {
            return;
        }

        // 光标还在输入框里时绑定可能还没回写,所以直接读输入框的文本再发
        viewModel.InputText = input.Text ?? string.Empty;

        if (viewModel.SendMessageCommand.CanExecute(null))
        {
            viewModel.SendMessageCommand.Execute(null);
            e.Handled = true;
        }
    }

    /// <summary>
    /// 输入条上 ✂ / ⊕ 的悬浮面板:Oopz 是悬浮弹出来的面板。
    /// 面板和小图标之间隔着 19px,鼠标从图标挪到面板上会经过这段空隙,
    /// 所以离开时不是立刻收,而是等 250ms;期间进到面板里就取消,这样鼠标才能移上去。
    /// </summary>
    private readonly DispatcherTimer _panelHideTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };

    private void OnScissorsPointerEntered(object? sender, Avalonia.Input.PointerEventArgs e) => ShowPanel(ScissorsPanel);

    private void OnScissorsPointerExited(object? sender, Avalonia.Input.PointerEventArgs e) => HidePanelLater();

    private void OnMorePointerEntered(object? sender, Avalonia.Input.PointerEventArgs e) => ShowPanel(MorePanel);

    private void OnMorePointerExited(object? sender, Avalonia.Input.PointerEventArgs e) => HidePanelLater();

    private void OnPanelPointerEntered(object? sender, Avalonia.Input.PointerEventArgs e) => _panelHideTimer.Stop();

    private void OnPanelPointerExited(object? sender, Avalonia.Input.PointerEventArgs e) => HidePanelLater();

    private void ShowPanel(Border panel)
    {
        _panelHideTimer.Stop();
        ScissorsPanel.IsVisible = ReferenceEquals(panel, ScissorsPanel);
        MorePanel.IsVisible = ReferenceEquals(panel, MorePanel);
    }

    private void HidePanelLater()
    {
        if (ScissorsPanel.IsPointerOver || MorePanel.IsPointerOver)
        {
            return;
        }

        _panelHideTimer.Start();
    }

    private void HidePanels()
    {
        ScissorsPanel.IsVisible = false;
        MorePanel.IsVisible = false;
    }


    /// <summary>点 ID 复制到剪贴板(Avalonia 12 的 IClipboard 没有 SetTextAsync,直接走 Win32)。</summary>
    private void OnCopyGroupIdPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (_viewModel?.Group.Id is not { Length: > 0 } id)
        {
            return;
        }

        if (!OpenClipboard(IntPtr.Zero))
        {
            return;
        }

        try
        {
            EmptyClipboard();
            SetClipboardData(13, System.Runtime.InteropServices.Marshal.StringToHGlobalUni(id));
        }
        finally
        {
            CloseClipboard();
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool OpenClipboard(IntPtr owner);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool EmptyClipboard();

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr SetClipboardData(uint format, IntPtr data);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool CloseClipboard();
}
