using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using nook_desktop.Core.Shell;
using nook_desktop.Services;
using nook_desktop.ViewModels;
using nook_desktop.ViewModels.Groups;
using nook_desktop.ViewModels.Room;

namespace nook_desktop;

public partial class MainWindow : Window, IWindowService
{
    private WindowEdgeResizer? _resizer;
    private TitlePanelController? _panels;
    private MainWindowViewModel? _viewModel;
    private GroupsViewModel? _groups;

    /// <summary>左栏竖线是否已经摆好;没摆好时每次布局重算,摆好就停。</summary>
    private bool _groupIndicatorPlaced;
    /// <summary>竖线是否已经滑到底并淡出(私信页那种没有选中域的状态),避免重复触发。</summary>
    private bool _groupIndicatorHidden = true;

    /// <summary>域菜单是否已经摆好(要等它量出高度才能夹在窗口内)。</summary>
    private bool _groupMenuAligned;

    /// <summary>域菜单弹出时的鼠标位置(菜单跟着点击处走)。</summary>
    private Point _groupMenuOrigin;

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
            MicButton,
            HeadphoneButton,
            MicPanelHost,
            HeadphonePanelHost);

        _groups = viewModel.Groups;

        _groups.PropertyChanged += (_, e) =>
        {
            // 弹窗状态一变就播缩放动画(冲过头再回落 / 缩回去)
            if (e.PropertyName == nameof(GroupsViewModel.IsSearchOpen))
            {
                _ = PlayDialogAnimation(SearchDialog, opening: _groups.IsSearchOpen);
            }
            else if (e.PropertyName == nameof(GroupsViewModel.IsGroupMenuOpen))
            {
                _ = PlayMenuAnimation(GroupMenu, _groups.IsGroupMenuOpen);
            }
            else if (e.PropertyName == nameof(GroupsViewModel.IsCreateGroupDialogOpen))
            {
                _ = PlayDialogAnimation(CreateGroupDialog, opening: _groups.IsCreateGroupDialogOpen);
            }
        };

        _groups.ActiveGroupChanged += (_, _) =>
        {
            _groupIndicatorPlaced = false;
            UpdateGroupIndicator();
        };

        LayoutUpdated += (_, _) =>
        {
            if (!_groupIndicatorPlaced)
            {
                UpdateGroupIndicator();
            }

            if (!_groupMenuAligned)
            {
                AlignGroupMenu();
            }
        };
        // 增删群组会把后面的项整体挪位,等这轮布局跑完再算一次
        // 增删 / 置顶会把两组的项整体挪位,等这轮布局跑完再算一次
        void Realign(object? _, System.Collections.Specialized.NotifyCollectionChangedEventArgs __) =>
            Dispatcher.UIThread.Post(UpdateGroupIndicator, DispatcherPriority.Loaded);

        _groups.Groups.CollectionChanged += Realign;
        _groups.PinnedGroups.CollectionChanged += Realign;


    }


    // ---------------------------------------------------------------- 弹窗动画

    /// <summary>弹出动画单步时长;两步加起来约 0.5s,和 OOPZ 实测一致。</summary>
    private static readonly TimeSpan PopStep = TimeSpan.FromMilliseconds(250);

    /// <summary>弹窗缩放的两条过渡(X / Y),只建一次复用。</summary>
    private static readonly Transitions DialogScaleTransitions = new()
    {
        new DoubleTransition { Property = ScaleTransform.ScaleXProperty, Duration = PopStep, Easing = new CubicEaseOut() },
        new DoubleTransition { Property = ScaleTransform.ScaleYProperty, Duration = PopStep, Easing = new CubicEaseOut() },
    };

    /// <summary>
    /// 弹窗缩放:打开时先冲过最终大小再回落(0.85 → 1.1 → 1),关闭时缩回去。
    /// 时长和过冲量照 OOPZ 实测:约 250ms 冲到峰值(比最终大约一成),再 250ms 落定。
    /// 走 ScaleTransform 自己的 Transitions —— 不能用 Animation.RunAsync:
    /// Avalonia 12 的 RunAsync 只接受 Visual,而 RenderTransform 属性又没有内置动画器。
    /// </summary>
    private static async Task PlayDialogAnimation(Border card, bool opening)
    {
        if (card.RenderTransform is not ScaleTransform scale)
        {
            return;
        }

        // 起点要瞬间落定:先摘掉过渡写值,再挂回去,后面两步才会走过渡。
        scale.Transitions = null;
        Set(scale, opening ? 0.85d : 1d);
        scale.Transitions = DialogScaleTransitions;

        if (opening)
        {
            Set(scale, 1.1d);
            await Task.Delay(PopStep);
            Set(scale, 1d);
        }
        else
        {
            Set(scale, 0.85d);
        }

        static void Set(ScaleTransform transform, double value)
        {
            transform.ScaleX = value;
            transform.ScaleY = value;
        }
    }

    /// <summary>菜单弹入:瞬间缩到 0.9,再冲过 1 一点回落到 1(玻璃菜单的入场,比弹窗快)。</summary>
    private static async Task PlayMenuAnimation(Border menu, bool opening)
    {
        if (menu.RenderTransform is not ScaleTransform scale)
        {
            return;
        }

        var steps = new Transitions
        {
            new DoubleTransition { Property = ScaleTransform.ScaleXProperty, Duration = TimeSpan.FromMilliseconds(130), Easing = new CubicEaseOut() },
            new DoubleTransition { Property = ScaleTransform.ScaleYProperty, Duration = TimeSpan.FromMilliseconds(130), Easing = new CubicEaseOut() },
        };

        scale.Transitions = null;
        MenuScale(scale, opening ? 0.9d : 1d);
        scale.Transitions = steps;

        if (opening)
        {
            MenuScale(scale, 1.04d);
            await Task.Delay(70);
            MenuScale(scale, 1d);
        }
        else
        {
            MenuScale(scale, 0.94d);
        }

        static void MenuScale(ScaleTransform transform, double value)
        {
            transform.ScaleX = value;
            transform.ScaleY = value;
        }
    }
    // ---------------------------------------------------------------- 域菜单

    /// <summary>右键域图标:开窗口内的域菜单。</summary>
    private void OnGroupItemPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsRightButtonPressed || _viewModel is null)
        {
            return;
        }

        if (sender is Control { DataContext: GroupCardViewModel card })
        {
            var origin = e.GetPosition(this);
            // 域里那两个菜单(群组设置 / 频道右键)也要一起收掉,不然会同时开着三个菜单
            if (_viewModel?.CurrentPage is RoomViewModel room)
            {
                room.CloseGroupSettingsMenuCommand.Execute(null);
                room.CloseChannelMenuCommand.Execute(null);
            }

            // 先收起旧菜单,再等这一帧渲染完抓底图,最后才弹新菜单(玻璃里不会留下上一个菜单)
            _viewModel?.Groups.CloseGroupMenuCommand.Execute(null);
            Dispatcher.UIThread.Post(
                () =>
                {
                    CaptureMenuBackdrop();
                    _groupMenuOrigin = origin;
                    _viewModel?.Groups.OpenGroupMenu(card);
                    _groupMenuAligned = false;
                },
                DispatcherPriority.Render);
        }

        e.Handled = true;
    }

    private void OnGroupItemPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is not Control { DataContext: GroupCardViewModel card } item
            || item.TranslatePoint(new Point(0, 0), this) is not { } origin)
        {
            return;
        }

        GroupHoverLabelText.Text = card.Name;
        GroupHoverLabel.Margin = new Thickness(origin.X + item.Bounds.Width + 10, origin.Y + 8, 0, 0);
        GroupHoverLabel.IsVisible = true;
    }

    private void OnGroupItemPointerExited(object? sender, PointerEventArgs e) => GroupHoverLabel.IsVisible = false;

    /// <summary>域菜单跟着鼠标点下的位置弹出,超出窗口就往回收。</summary>
    private RenderTargetBitmap? _menuSnapshot;

    /// <summary>菜单玻璃底图:开菜单前把整窗渲一张,菜单那块裁出来糊掉(不糊别处)。</summary>
    private void CaptureMenuBackdrop()
    {
        var scale = RenderScaling;
        var size = new PixelSize(
            Math.Max(1, (int)Math.Ceiling(Bounds.Width * scale)),
            Math.Max(1, (int)Math.Ceiling(Bounds.Height * scale)));

        var bitmap = new RenderTargetBitmap(size, new Vector(96 * scale, 96 * scale));
        bitmap.Render(this);
        _menuSnapshot = bitmap;
    }
    private void AlignGroupMenu()
    {
        if (_groups is not { IsGroupMenuOpen: true } || GroupMenu.Bounds.Height <= 0)
        {
            _groupMenuAligned = false;
            return;
        }

        var x = Math.Min(_groupMenuOrigin.X, Math.Max(0, Bounds.Width - GroupMenu.Bounds.Width - 8));
        var y = Math.Min(_groupMenuOrigin.Y, Math.Max(0, Bounds.Height - GroupMenu.Bounds.Height - 8));

        GroupMenu.Margin = new Thickness(x, y, 0, 0);
        _groupMenuAligned = true;

        // 玻璃层:把快照按菜单的位置裁出来(菜单内边距 6)
        GroupMenuGlassImage.Source = _menuSnapshot;
        GroupMenuGlass.Width = Math.Max(0, GroupMenu.Bounds.Width - 12);
        GroupMenuGlass.Height = Math.Max(0, GroupMenu.Bounds.Height - 12);

        if (GroupMenuGlassImage.RenderTransform is TranslateTransform glassOffset
            && GroupMenuGlass.TranslatePoint(new Point(0, 0), this) is { } glassOrigin)
        {
            glassOffset.X = -glassOrigin.X;
            glassOffset.Y = -glassOrigin.Y;
        }
    }

    // ---------------------------------------------------------------- 左栏竖线

    /// <summary>把竖线移到当前群组那一行。位置相对滚动内容,所以滚动和窗口缩放都不用重算。</summary>
    private void UpdateGroupIndicator()
    {
        var index = -1;
        var list = GroupList;

        if (_groups is { ActiveGroup: { } card })
        {
            // 置顶的域在上面那个列表里,普通域在下面那个;竖线跟着它所在的那一份找行
            var pinned = _groups.PinnedGroups.IndexOf(card);

            if (pinned >= 0)
            {
                index = pinned;
                list = PinnedGroupList;
            }
            else
            {
                index = _groups.Groups.IndexOf(card);
            }
        }

        if (index < 0)
        {
            HideGroupIndicator();
            return;
        }

        if (list.ContainerFromIndex(index) is not { } container
            || GroupIndicator.Parent is not Visual layer
            || container.TranslatePoint(new Point(0, 0), layer) is not { } position
            || GroupIndicator.RenderTransform is not TranslateTransform offset)
        {
            // 行还没生成(比如启动直接进域页):先不画,等下一轮布局再试
            GroupIndicator.IsVisible = false;
            _groupIndicatorPlaced = false;
            return;
        }

        _groupIndicatorHidden = false;
        offset.Y = position.Y + (container.Bounds.Height - GroupIndicator.Height) / 2;
        GroupIndicator.Opacity = 1;
        GroupIndicator.IsVisible = true;
        _groupIndicatorPlaced = true;
    }

    /// <summary>
    /// 切到私信页(没有选中的域):竖线先往下滑出域列表(停在最后一行域的下面一行),
    /// 滑的同时慢慢淡出;回到域页时再滑回来、同时淡入。
    /// </summary>
    private void HideGroupIndicator()
    {
        if (_groupIndicatorHidden)
        {
            return;
        }

        _groupIndicatorHidden = true;
        _groupIndicatorPlaced = true;

        // 域列表是自下而上排的,所以"最后一个域"= 屏幕上最下面那一行:取 Y 最大的行,
        // 再往下挪一行(划出列表,但不真的滑到底部),然后淡出。
        var lowestY = double.NaN;
        var rowHeight = 0d;

        // 两个列表都扫:屏幕上最下面那一行可能在置顶组里
        foreach (var rows in new[] { PinnedGroupList, GroupList })
        {
            for (var i = 0; i < rows.ItemCount; i++)
            {
                if (rows.ContainerFromIndex(i) is not { } row
                    || GroupIndicator.Parent is not Visual layer
                    || row.TranslatePoint(new Point(0, 0), layer) is not { } origin)
                {
                    continue;
                }

                var y = origin.Y + (row.Bounds.Height - GroupIndicator.Height) / 2;

                if (double.IsNaN(lowestY) || y > lowestY)
                {
                    lowestY = y;
                    rowHeight = row.Bounds.Height;
                }
            }
        }

        if (!double.IsNaN(lowestY) && GroupIndicator.RenderTransform is TranslateTransform offset)
        {
            offset.Y = lowestY + rowHeight + 12;
        }

        _ = FadeOutIndicatorAsync();

        async Task FadeOutIndicatorAsync()
        {
            await Task.Delay(220);   // 等滑完再淡出

            if (_groupIndicatorHidden)
            {
                GroupIndicator.Opacity = 0;
            }
        }
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
        // 同一时刻只留一个菜单:域菜单除了"右键在域栏上换目标"这种情况,其它按下都收起来
        if (_groups is { IsGroupMenuOpen: true })
        {
            var onRail = e.GetCurrentPoint(this).Properties.IsRightButtonPressed
                && e.Source is Visual hit
                && (IsInside(hit, GroupList) || IsInside(hit, PinnedGroupList));

            if (!onRail)
            {
                _viewModel?.Groups.CloseGroupMenuCommand.Execute(null);
            }
        }

        if (_resizer?.TryBegin(e) == true)
        {
            e.Handled = true;
            return;
        }

        // 点到不可聚焦的空白处:把焦点收走,输入框不该一直保持选中态
        if (FocusManager?.GetFocusedElement() is not null
            && e.Source is Visual source
            && !HasFocusableAncestor(source))
        {
            BlankFocus.Focus();
        }
    }

    /// <summary>从点到的地方往上找(不含窗口本身),链上有可聚焦控件就说明这一下会自己处理焦点。</summary>
    private static bool IsInside(Visual source, Visual target) => source.GetSelfAndVisualAncestors().Contains(target);

    private static bool HasFocusableAncestor(Visual source) => source
        .GetSelfAndVisualAncestors()
        .TakeWhile(element => element is not TopLevel)
        .OfType<InputElement>()
        .Any(element => element.Focusable);

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

    /// <summary>一次缩放拖拽松手时触发(拖动过程中不触发),用来把尺寸写回配置。</summary>
    public event EventHandler? ResizeCompleted;

    private void OnWindowPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (EndResize(e.Pointer))
        {
            ResizeCompleted?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>结束缩放,返回这次是否真的在缩放中。</summary>
    private bool EndResize(IPointer? pointer)
    {
        if (_resizer is not { IsResizing: true })
        {
            return false;
        }

        _resizer.End(pointer);
        return true;
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

        if (EndResize(null))
        {
            ResizeCompleted?.Invoke(this, EventArgs.Empty);
        }
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

    /// <summary>点遮罩关掉搜索窗口。</summary>
    private void OnSearchScrimPressed(object? sender, PointerPressedEventArgs e)
    {
        CloseSearch();
        e.Handled = true;
    }

    /// <summary>点弹窗空白处:把焦点收回到卡片,输入框就不再是「使用中」的样子。</summary>
    private void OnSearchDialogPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control control)
        {
            control.Focus();
        }
    }

    /// <summary>点进输入框:包边提亮,而不是 Fluent 那圈蓝色焦点。</summary>
    private void OnSearchInputGotFocus(object? sender, RoutedEventArgs e) =>
        SearchInputPill.Classes.Set("rim", true);

    private void OnSearchInputLostFocus(object? sender, RoutedEventArgs e) =>
        SearchInputPill.Classes.Set("rim", false);

    private void OnSearchInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CloseSearch();
            e.Handled = true;
        }
    }

    private void CloseSearch() => _groups?.CloseSearchCommand.Execute(null);

    /// <summary>点弹窗外面关掉创建 / 加入域弹窗。</summary>
    private void OnCreateGroupScrimPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.Groups.CloseCreateGroupDialogCommand.Execute(null);
        }

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
