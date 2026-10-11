using System;
using Avalonia.Media;
using nook_desktop.Core.Mvvm;
using nook_desktop.Core.Navigation;
using nook_desktop.Models;
using nook_desktop.Services;
using nook_desktop.ViewModels.Friends;
using nook_desktop.ViewModels.Groups;
using nook_desktop.ViewModels.Messages;
using nook_desktop.ViewModels.Room;
using nook_desktop.ViewModels.Settings;
using nook_desktop.ViewModels.TitleBar;

namespace nook_desktop.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly INavigationService _navigation;
    private UserStatus _userStatus = UserStatus.Online;
    private TitlePanelKind _openPanel = TitlePanelKind.None;

    public MainWindowViewModel(AppServices services)
    {
        _navigation = new NavigationService();

        Groups = new GroupsViewModel(_navigation, services);
        Friends = new FriendsViewModel();
        Messages = new MessagesViewModel();
        Settings = new SettingsViewModel(_navigation, services.Settings);

        MicPanel = new MicPanelViewModel(services.AudioDevice, services.VoiceSession, services.Settings);
        HeadphonePanel = new HeadphonePanelViewModel(services.AudioDevice, services.VoiceSession, services.Settings);
        KeyboardPanel = new KeyboardPanelViewModel(services.AudioDevice);

        MinimizeCommand = new RelayCommand(() => Window?.Minimize());
        MaximizeCommand = new RelayCommand(() => Window?.ToggleMaximize());
        CloseCommand = new RelayCommand(() => Window?.Close());
        OpenSettingsCommand = new RelayCommand(() => _navigation.NavigateTo(Settings));

        ShowGroupsCommand = new RelayCommand(() => GoToShell(Groups));
        ShowFriendsCommand = new RelayCommand(() => GoToShell(Friends));
        ShowMessagesCommand = new RelayCommand(() => GoToShell(Messages));

        _navigation.Navigated += (_, _) => RefreshNavState();
        _navigation.NavigateTo(Groups);
    }

    public GroupsViewModel Groups { get; }

    public FriendsViewModel Friends { get; }

    public MessagesViewModel Messages { get; }

    public SettingsViewModel Settings { get; }

    public MicPanelViewModel MicPanel { get; }

    public HeadphonePanelViewModel HeadphonePanel { get; }

    public KeyboardPanelViewModel KeyboardPanel { get; }

    public object? CurrentPage => _navigation.Current;

    public IWindowService? Window { get; set; }

    /// <summary>手动设置的用户状态,默认在线。头像右下角的状态点用它。</summary>
    public UserStatus UserStatus
    {
        get => _userStatus;
        set
        {
            if (SetProperty(ref _userStatus, value))
            {
                OnPropertyChanged(nameof(UserStatusBrush));
            }
        }
    }

    public IBrush UserStatusBrush => new SolidColorBrush(Color.Parse(UserStatus switch
    {
        UserStatus.Online => "#3DD68C",
        UserStatus.Away => "#F5A623",
        UserStatus.Busy => "#F2555A",
        _ => "#6C7183",
    }));

    // ---- 标题栏浮层 ------------------------------------------------------
    // 菜单不使用 Flyout(Flyout 是独立顶层窗口,会画到窗口外),
    // 而是在窗口内部渲染一层浮层。这里只记录当前展开的是哪一块。
    public TitlePanelKind OpenPanel
    {
        get => _openPanel;
        private set
        {
            if (!SetProperty(ref _openPanel, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsKeyboardPanelOpen));
            OnPropertyChanged(nameof(IsMicPanelOpen));
            OnPropertyChanged(nameof(IsHeadphonePanelOpen));
            OnPropertyChanged(nameof(IsAnyPanelOpen));
        }
    }

    public bool IsKeyboardPanelOpen => _openPanel == TitlePanelKind.Keyboard;

    public bool IsMicPanelOpen => _openPanel == TitlePanelKind.Mic;

    public bool IsHeadphonePanelOpen => _openPanel == TitlePanelKind.Headphone;

    public bool IsAnyPanelOpen => _openPanel != TitlePanelKind.None;

    /// <summary>
    /// 频道页与设置页是「左右分区」布局,左侧有一条竖向分隔线。
    /// 标题栏那一段由 MainWindow 补画,这样竖线能从窗口顶部一直连到页面底部。
    /// </summary>
    public bool ShowLeftDivider => _navigation.Current is RoomViewModel or SettingsViewModel;

    public void ShowPanel(TitlePanelKind panel) => OpenPanel = panel;

    public void HidePanels() => OpenPanel = TitlePanelKind.None;

    public RelayCommand MinimizeCommand { get; }

    public RelayCommand MaximizeCommand { get; }

    public RelayCommand CloseCommand { get; }

    public RelayCommand OpenSettingsCommand { get; }

    public RelayCommand ShowGroupsCommand { get; }

    public RelayCommand ShowFriendsCommand { get; }

    public RelayCommand ShowMessagesCommand { get; }

    public bool IsGroupsActive { get; private set; }

    public bool IsFriendsActive { get; private set; }

    public bool IsMessagesActive { get; private set; }

    /// <summary>
    /// 从二级页面(群组内、设置)返回顶层页面时复用已有实例,避免重复压栈。
    /// </summary>
    private void GoToShell(object target)
    {
        if (_navigation.Current is RoomViewModel)
        {
            _navigation.GoBack();
            return;
        }

        if (!ReferenceEquals(_navigation.Current, target))
        {
            _navigation.NavigateTo(target);
        }
    }

    private void RefreshNavState()
    {
        IsGroupsActive = _navigation.Current is GroupsViewModel or RoomViewModel;
        IsFriendsActive = _navigation.Current is FriendsViewModel;
        IsMessagesActive = _navigation.Current is MessagesViewModel;

        OnPropertyChanged(nameof(CurrentPage));
        OnPropertyChanged(nameof(ShowLeftDivider));
        OnPropertyChanged(nameof(IsGroupsActive));
        OnPropertyChanged(nameof(IsFriendsActive));
        OnPropertyChanged(nameof(IsMessagesActive));
    }
}

/// <summary>标题栏可展开的浮层。用枚举代替字符串,避免拼写错误和魔法值比较。</summary>
public enum TitlePanelKind
{
    None,
    Keyboard,
    Mic,
    Headphone,
}