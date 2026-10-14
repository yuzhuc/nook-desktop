using System;
using Avalonia.Media;
using nook_desktop.Core.Mvvm;
using nook_desktop.Core.Navigation;
using nook_desktop.Models;
using nook_desktop.Services;
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
        Messages = new MessagesViewModel(services.Chat);
        Settings = new SettingsViewModel(_navigation, services.Settings);

        MicPanel = new MicPanelViewModel(services.AudioDevice, services.VoiceSession, services.Settings);
        HeadphonePanel = new HeadphonePanelViewModel(services.AudioDevice, services.VoiceSession, services.Settings);

        MinimizeCommand = new RelayCommand(() => Window?.Minimize());
        MaximizeCommand = new RelayCommand(() => Window?.ToggleMaximize());
        CloseCommand = new RelayCommand(() => Window?.Close());
        OpenSettingsCommand = new RelayCommand(() => _navigation.NavigateTo(Settings));

        ShowMessagesCommand = new RelayCommand(() => GoToShell(Messages));

        // 页面内的标签切换不触发导航,标题栏那个私信按钮的选中态要跟着刷新
        Messages.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MessagesViewModel.Tab))
            {
                RefreshNavState();
            }
        };

        _navigation.Navigated += (_, _) => RefreshNavState();

        // 没域可开时回到私信页
        Groups.ShowMessagesRequested += (_, _) => GoToShell(Messages);

        // 启动:有域就进最上面那个域,一个都没有才停在私信页
        Groups.OpenFirstGroup();
    }

    public GroupsViewModel Groups { get; }

    public MessagesViewModel Messages { get; }

    public SettingsViewModel Settings { get; }

    public MicPanelViewModel MicPanel { get; }

    public HeadphonePanelViewModel HeadphonePanel { get; }

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

            OnPropertyChanged(nameof(IsMicPanelOpen));
            OnPropertyChanged(nameof(IsHeadphonePanelOpen));
            OnPropertyChanged(nameof(IsAnyPanelOpen));
        }
    }

    public bool IsMicPanelOpen => _openPanel == TitlePanelKind.Mic;

    public bool IsHeadphonePanelOpen => _openPanel == TitlePanelKind.Headphone;

    public bool IsAnyPanelOpen => _openPanel != TitlePanelKind.None;

    /// <summary>
    /// 频道页、设置页与消息页是「左右分区」布局,左侧有一条竖向分隔线。
    /// 标题栏那一段由 MainWindow 补画,这样竖线能从窗口顶部一直连到页面底部。
    /// </summary>
    public bool ShowLeftDivider => _navigation.Current is RoomViewModel or SettingsViewModel or MessagesViewModel;

    public void ShowPanel(TitlePanelKind panel) => OpenPanel = panel;

    public void HidePanels() => OpenPanel = TitlePanelKind.None;

    public RelayCommand MinimizeCommand { get; }

    public RelayCommand MaximizeCommand { get; }

    public RelayCommand CloseCommand { get; }

    public RelayCommand OpenSettingsCommand { get; }

    public RelayCommand ShowMessagesCommand { get; }

    public bool IsMessagesActive => _navigation.Current is MessagesViewModel { Tab: MessageTab.Private };


    /// <summary>
    /// 从房间/设置回到顶层页面时复用已有实例,避免重复压栈。
    /// </summary>
    private void GoToShell(object target)
    {
        if (ReferenceEquals(_navigation.Current, target))
        {
            return;
        }

        // 房间可以连着开好几个,只回退一次不保证回到顶层页面
        if (_navigation.Current is RoomViewModel)
        {
            _navigation.GoBack();
            if (ReferenceEquals(_navigation.Current, target))
            {
                return;
            }
        }

        _navigation.NavigateTo(target);
    }

    private void RefreshNavState()
    {
        OnPropertyChanged(nameof(CurrentPage));
        OnPropertyChanged(nameof(ShowLeftDivider));
        OnPropertyChanged(nameof(IsMessagesActive));
    }
}

/// <summary>标题栏可展开的浮层。用枚举代替字符串,避免拼写错误和魔法值比较。</summary>
public enum TitlePanelKind
{
    None,
    Mic,
    Headphone,
}