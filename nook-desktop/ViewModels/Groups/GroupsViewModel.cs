using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media;
using nook_desktop.Core.Mvvm;
using nook_desktop.Core.Navigation;
using nook_desktop.Models;
using nook_desktop.Services;
using nook_desktop.ViewModels.Room;

namespace nook_desktop.ViewModels.Groups;

public sealed class GroupsViewModel : ViewModelBase
{
    private readonly INavigationService _navigation;
    private readonly AppServices _services;
    // 新增群组时轮流取的主色,和演示数据用的是同一组颜色
    private static readonly string[] AccentPalette = { "#3B9EFF", "#3DD68C", "#F5A623", "#F2555A" };

    private readonly ObservableCollection<Group> _sourceGroups = new();
    private string _searchText = string.Empty;
    private bool _isSearchOpen;
    private bool _isGroupMenuOpen;
    private GroupCardViewModel? _menuGroup;
    private bool _isCreateGroupDialogOpen;
    private bool _isCreatingGroup;
    private string _newGroupName = string.Empty;

    public GroupsViewModel(INavigationService navigation, AppServices services)
    {
        _navigation = navigation;
        _services = services;

        Groups = new ObservableCollection<GroupCardViewModel>();
        PinnedGroups = new ObservableCollection<GroupCardViewModel>();
        Reload();
        _navigation.Navigated += (_, _) => UpdateActiveGroup();

        JoinGroupCommand = new AsyncRelayCommand<GroupCardViewModel>(JoinGroupAsync);
        CloseGroupMenuCommand = new RelayCommand(() => IsGroupMenuOpen = false);
        PinMenuGroupCommand = new RelayCommand(PinMenuGroup);
        OpenSearchCommand = new RelayCommand(() => IsSearchOpen = true);
        CloseSearchCommand = new RelayCommand(() => IsSearchOpen = false);
        OpenCreateGroupDialogCommand = new RelayCommand(OpenCreateGroupDialog);
        CloseCreateGroupDialogCommand = new RelayCommand(() => IsCreateGroupDialogOpen = false);
        ShowCreateGroupStepCommand = new RelayCommand(() => IsCreatingGroup = true);
        BackToChoiceCommand = new RelayCommand(() => IsCreatingGroup = false);
        CreateGroupCommand = new RelayCommand(CreateGroup, () => !string.IsNullOrWhiteSpace(NewGroupName));
    }

    public ObservableCollection<GroupCardViewModel> Groups { get; }

    /// <summary>置顶的域:最新的置顶在最上面。</summary>
    public ObservableCollection<GroupCardViewModel> PinnedGroups { get; }

    /// <summary>置顶组和普通组之间要不要画分割线(两边都有域才画)。</summary>
    public bool ShowRailDivider => PinnedGroups.Count > 0 && Groups.Count > 0;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFilter();
            }
        }
    }

    public AsyncRelayCommand<GroupCardViewModel> JoinGroupCommand { get; }

    public RelayCommand CloseGroupMenuCommand { get; }

    /// <summary>域菜单里的「置顶域」:把它顶到域栏最上面,序号落库所以重启还在。</summary>
    public RelayCommand PinMenuGroupCommand { get; }

    /// <summary>菜单当前对应的域,窗口按它定位。</summary>
    public GroupCardViewModel? MenuGroup => _menuGroup;

    /// <summary>菜单里那条是「置顶域」还是「取消置顶」,看当前右键的域是不是已经置顶。</summary>
    public string PinMenuLabel => _menuGroup?.Group.IsPinned == true ? "取消置顶" : "置顶域";

    /// <summary>域菜单(窗口内浮层)是否打开。</summary>
    public bool IsGroupMenuOpen
    {
        get => _isGroupMenuOpen;
        private set
        {
            if (SetProperty(ref _isGroupMenuOpen, value))
            {
                OnPropertyChanged(nameof(OverlayBlurRadius));
            }
        }
    }

    public RelayCommand OpenSearchCommand { get; }

    public RelayCommand CloseSearchCommand { get; }

    /// <summary>搜索弹窗是否打开。</summary>
    public bool IsSearchOpen
    {
        get => _isSearchOpen;
        private set
        {
            if (SetProperty(ref _isSearchOpen, value))
            {
                OnPropertyChanged(nameof(OverlayBlurRadius));
            }
        }
    }

    /// <summary>弹窗打开时背景的模糊半径;没弹窗就是 0,等于不模糊。</summary>
    public double OverlayBlurRadius => IsSearchOpen || IsCreateGroupDialogOpen ? 16 : 0;

    public RelayCommand OpenCreateGroupDialogCommand { get; }

    public RelayCommand CloseCreateGroupDialogCommand { get; }

    /// <summary>从「创建或加入域」切到「创建域」表单。</summary>
    public RelayCommand ShowCreateGroupStepCommand { get; }

    public RelayCommand BackToChoiceCommand { get; }

    public RelayCommand CreateGroupCommand { get; }

    /// <summary>创建 / 加入域弹窗是否打开。</summary>
    public bool IsCreateGroupDialogOpen
    {
        get => _isCreateGroupDialogOpen;
        private set
        {
            if (SetProperty(ref _isCreateGroupDialogOpen, value))
            {
                OnPropertyChanged(nameof(OverlayBlurRadius));
            }
        }
    }

    /// <summary>false = 「创建或加入域」选择页,true = 「创建域」表单页。</summary>
    public bool IsCreatingGroup
    {
        get => _isCreatingGroup;
        private set => SetProperty(ref _isCreatingGroup, value);
    }

    public string NewGroupName
    {
        get => _newGroupName;
        set
        {
            if (SetProperty(ref _newGroupName, value))
            {
                CreateGroupCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>当前停在哪个群组的房间页。左栏那条竖线看它。</summary>
    public GroupCardViewModel? ActiveGroup { get; private set; }

    /// <summary>选中的群组换人时通知一次(切到没有选中项的页面时也会通知)。</summary>
    public event EventHandler? ActiveGroupChanged;

    /// <summary>没有域可以打开时(比如删掉了最后一个域)请外壳切到私信页。</summary>
    public event EventHandler? ShowMessagesRequested;

    /// <summary>右键域图标:打开窗口内的域菜单。</summary>
    public void OpenGroupMenu(GroupCardViewModel? card)
    {
        _menuGroup = card;
        OnPropertyChanged(nameof(PinMenuLabel));
        IsGroupMenuOpen = card is not null;
    }


    /// <summary>域菜单里那条:没置顶就置顶(排到置顶组第一个),已置顶就取消置顶。</summary>
    private void PinMenuGroup()
    {
        if (_menuGroup is { } card)
        {
            if (card.Group.IsPinned)
            {
                _services.Groups.Unpin(card.Group.Id);
            }
            else
            {
                _services.Groups.Pin(card.Group.Id);
            }

            Reload();
        }

        IsGroupMenuOpen = false;
    }

    private void OpenCreateGroupDialog()
    {
        NewGroupName = string.Empty;
        IsCreatingGroup = false;
        IsCreateGroupDialogOpen = true;
    }

    /// <summary>建完就关弹窗并刷新左栏;ID 和默认频道由存储负责。</summary>
    private void CreateGroup()
    {
        var name = NewGroupName.Trim();
        if (name.Length == 0)
        {
            return;
        }

        _services.Groups.Add(name, AccentPalette[_sourceGroups.Count % AccentPalette.Length]);
        Reload();
        IsCreateGroupDialogOpen = false;
    }

    private void DeleteGroup(GroupCardViewModel card)
    {
        // 先记住:删的是不是当前正开着的域,以及它原来的位置
        var wasOpen = _navigation.Current is RoomViewModel room && room.Group.Id == card.Group.Id;
        var index = _sourceGroups.IndexOf(card.Group);

        _services.Groups.Delete(card.Group.Id);
        Reload();

        if (!wasOpen)
        {
            return;
        }

        // 顶上来的是原位置那一个;删的是最后一个就退回新的最后一个
        var next = _sourceGroups.Count == 0
            ? null
            : _sourceGroups[Math.Min(index, _sourceGroups.Count - 1)];

        if (next is null)
        {
            ShowMessagesRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        _ = OpenGroupAsync(next);
    }

    /// <summary>打开列表最上面的域(显示是倒序的,所以取最后一个);一个都没有就回私信页。</summary>
    public void OpenFirstGroup()
    {
        if (Groups.LastOrDefault() is { } card)
        {
            _ = OpenGroupAsync(card.Group);
            return;
        }

        ShowMessagesRequested?.Invoke(this, EventArgs.Empty);
    }

    private async Task JoinGroupAsync(GroupCardViewModel? card)
    {
        if (card?.Group is not { } group)
        {
            return;
        }

        await OpenGroupAsync(group);
    }

    /// <summary>进某个域:点亮左栏竖线、进第一个语音频道、导航到房间页。</summary>
    private async Task OpenGroupAsync(Group group)
    {
        SetActiveGroup(group.Id);

        var voiceChannel = group.Categories
            .SelectMany(category => category.Channels)
            .Concat(group.LooseChannels)
            .FirstOrDefault(channel => channel.Kind == ChannelKind.Voice);

        if (voiceChannel is not null)
        {
            await _services.VoiceSession.JoinAsync(voiceChannel.Id);
        }

        _navigation.NavigateTo(new RoomViewModel(_navigation, _services, group));
    }

    private void ApplyFilter()
    {
        var source = _sourceGroups.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var keyword = SearchText.Trim();
            source = source.Where(g =>
                g.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                (g.Description?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        Groups.Clear();
        PinnedGroups.Clear();

        // 两组都按 position 倒序:置顶组里最新置顶的在最上,普通组里最后建的紧挨 + 按钮
        foreach (var group in source.Where(group => !group.IsPinned).Reverse())
        {
            Groups.Add(new GroupCardViewModel(group));
        }

        foreach (var group in source.Where(group => group.IsPinned).Reverse())
        {
            PinnedGroups.Add(new GroupCardViewModel(group));
        }

        OnPropertyChanged(nameof(ShowRailDivider));
        UpdateActiveGroup();
    }

    /// <summary>左栏竖线的位置:只有停在某个群组的房间页时才算「在里面」。</summary>
    private void UpdateActiveGroup() => SetActiveGroup((_navigation.Current as RoomViewModel)?.Group.Id);

    private void SetActiveGroup(string? groupId)
    {
        GroupCardViewModel? active = null;

        foreach (var card in PinnedGroups.Concat(Groups))
        {
            if (card.Group.Id == groupId)
            {
                active = card;
                break;
            }
        }

        if (ReferenceEquals(ActiveGroup, active))
        {
            return;
        }

        ActiveGroup = active;
        ActiveGroupChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Reload()
    {
        _sourceGroups.Clear();
        foreach (var group in _services.Groups.GetAll())
        {
            _sourceGroups.Add(group);
        }

        ApplyFilter();
    }
}

public sealed class GroupCardViewModel : ViewModelBase
{
    public GroupCardViewModel(Group group)
    {
        Group = group;
    }

    public Group Group { get; }

    public string Name => Group.Name;


    public IBrush AccentBrush => BuildBrush(Group.AccentColor);


    private static IBrush BuildBrush(string hex)
    {
        try
        {
            return new SolidColorBrush(Color.Parse(hex));
        }
        catch (FormatException)
        {
            return Brushes.SlateGray;
        }
    }
}
