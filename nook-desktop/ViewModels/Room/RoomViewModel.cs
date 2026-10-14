using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using nook_desktop.Core.Mvvm;
using nook_desktop.Core.Navigation;
using nook_desktop.Models;
using nook_desktop.Services;

namespace nook_desktop.ViewModels.Room;

public sealed class RoomViewModel : ViewModelBase
{
    /// <summary>主页那一项的「分组 id」,和真实分组 id 不会撞,用来判断切换时该不该做动画。</summary>
    private const string HomeCategoryId = "home";

    private readonly INavigationService _navigation;
    private readonly AppServices _services;
    private bool _isMuted;
    private bool _isDeafened;
    private string _inputText = string.Empty;
    private bool _isMemberListVisible = true;
    private bool _isGroupSettingsMenuOpen;
    private bool _isChannelMenuOpen;
    private string _memberSearchText = string.Empty;
    private bool _isMemberSearchOpen;
    private RoomChannelViewModel? _menuChannel;

    public RoomViewModel(INavigationService navigation, AppServices services, Group group)
    {
        _navigation = navigation;
        _services = services;
        Group = group;

        _isMuted = services.VoiceSession.IsMuted;
        _isDeafened = services.VoiceSession.IsDeafened;

        Members = new ObservableCollection<RoomMember>();
        Messages = new ObservableCollection<RoomMessage>();

        Categories = new ObservableCollection<RoomCategoryViewModel>();
        LooseChannels = new ObservableCollection<RoomChannelViewModel>();

        Home = new RoomChannelViewModel("home", "主页", HomeCategoryId, isVoice: false, capacityText: null, channel => SelectChannel(channel));
        Reload();

        // 收到的消息落到当前频道的列表里(占位服务会把自己发的也回推一遍)
        services.Chat.MessageReceived += (_, message) =>
        {
            if (message.ChannelId == SelectedChannel?.Id)
            {
                Append(message);
            }
        };

        ToggleMuteCommand = new AsyncRelayCommand(ToggleMuteAsync, () => IsConnected);
        ToggleDeafenCommand = new AsyncRelayCommand(ToggleDeafenAsync, () => IsConnected);
        SendMessageCommand = new AsyncRelayCommand(SendMessageAsync, () => !string.IsNullOrWhiteSpace(InputText));
        ToggleComposerCommand = new RelayCommand(() => IsComposerExpanded = !IsComposerExpanded);
        LeaveRoomCommand = new AsyncRelayCommand(LeaveRoomAsync);
        AddMemberCommand = new RelayCommand(AddMember);
        ToggleMemberListCommand = new RelayCommand(() => IsMemberListVisible = !IsMemberListVisible);
        OpenGroupSettingsMenuCommand = new RelayCommand(() =>
        {
            IsChannelMenuOpen = false;
            IsGroupSettingsMenuOpen = true;
        });
        CloseGroupSettingsMenuCommand = new RelayCommand(() => IsGroupSettingsMenuOpen = false);
        CreateChannelCommand = new RelayCommand(CreateChannel);
        CreateCategoryCommand = new RelayCommand(CreateCategory);
        CloseChannelMenuCommand = new RelayCommand(() => IsChannelMenuOpen = false);
        OpenMemberSearchCommand = new RelayCommand(() => IsMemberSearchOpen = true);
        CloseMemberSearchCommand = new RelayCommand(CloseMemberSearch);
        CopyChannelCommand = new RelayCommand(CopyChannel);
        DeleteChannelCommand = new RelayCommand(DeleteChannel);


        var voiceSession = services.VoiceSession;
        voiceSession.MuteChanged += (_, muted) => IsMuted = muted;
        voiceSession.DeafenChanged += (_, deafened) => IsDeafened = deafened;
        voiceSession.StateChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(IsConnected));
            ToggleMuteCommand.RaiseCanExecuteChanged();
            ToggleDeafenCommand.RaiseCanExecuteChanged();
        };
    }

    public Group Group { get; }

    public RoomChannelViewModel Home { get; }

    public ObservableCollection<RoomCategoryViewModel> Categories { get; }

    /// <summary>没归到分组里的频道。</summary>
    public ObservableCollection<RoomChannelViewModel> LooseChannels { get; }

    /// <summary>左栏选中的频道。中栏标题、输入条占位和语音跳转都看它。</summary>
    public RoomChannelViewModel? SelectedChannel { get; private set; }

    public bool SelectedIsVoice => SelectedChannel?.IsVoice == true;

    public ObservableCollection<RoomMember> Members { get; }

    /// <summary>成员栏搜索框里真正显示的成员(照 Oopz:输入即时过滤)。</summary>
    public ObservableCollection<RoomMember> VisibleMembers { get; } = new();

    /// <summary>成员栏那个「搜索成员」输入框。</summary>
    public string MemberSearchText
    {
        get => _memberSearchText;
        set
        {
            if (SetProperty(ref _memberSearchText, value))
            {
                ApplyMemberFilter();
            }
        }
    }

    /// <summary>成员分组标题。我们还没有在线/离线分组,所以照 Oopz 的「离线 - N」写法写成「在线 - N」。</summary>
    public string MemberGroupLabel => $"在线 - {VisibleMembers.Count}";

    /// <summary>成员栏那个搜索框开着没有。照 Oopz:点放大镜才出现,点别处就收起来。</summary>
    public bool IsMemberSearchOpen
    {
        get => _isMemberSearchOpen;
        private set
        {
            if (SetProperty(ref _isMemberSearchOpen, value))
            {
                OnPropertyChanged(nameof(MemberSearchLabelHeight));
                OnPropertyChanged(nameof(MemberSearchDropOffset));
                OnPropertyChanged(nameof(MemberHeaderOffset));
            }
        }
    }

    public RelayCommand OpenMemberSearchCommand { get; }

    public RelayCommand CloseMemberSearchCommand { get; }

    /// <summary>搜索态下分组标题那一行的高度:0 或 31,靠 XAML 里的过渡做成上下滑动(照 Oopz 那个点击动画)。</summary>
    public double MemberSearchLabelHeight => IsMemberSearchOpen ? 42 : 0;

    /// <summary>搜索框那块的纵向偏移:收起时在上方 -45,展开时滑到 0(窗帘式下拉)。</summary>
    public double MemberSearchDropOffset => IsMemberSearchOpen ? 0 : -45;

    /// <summary>常态抬头(在线 - N)的纵向偏移:开搜索时往上滑出去,和搜索框做窗帘式交换。</summary>
    public double MemberHeaderOffset => IsMemberSearchOpen ? 41 : 0;

    public ObservableCollection<RoomMessage> Messages { get; }

    public bool IsConnected => _services.VoiceSession.State == VoiceConnectionState.Connected;

    public bool IsMuted
    {
        get => _isMuted;
        private set
        {
            if (SetProperty(ref _isMuted, value))
            {
                OnPropertyChanged(nameof(MicLabel));
            }
        }
    }

    public bool IsDeafened
    {
        get => _isDeafened;
        private set
        {
            if (SetProperty(ref _isDeafened, value))
            {
                OnPropertyChanged(nameof(DeafenLabel));
            }
        }
    }

    public string MicLabel => IsMuted ? "取消静音" : "静音";

    public string DeafenLabel => IsDeafened ? "取消静音听" : "静音听";

    public string InputText
    {
        get => _inputText;
        set
        {
            if (SetProperty(ref _inputText, value))
            {
                SendMessageCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public AsyncRelayCommand ToggleMuteCommand { get; }

    public AsyncRelayCommand ToggleDeafenCommand { get; }

    public AsyncRelayCommand SendMessageCommand { get; }

    public RelayCommand ToggleComposerCommand { get; }

    private bool _isComposerExpanded;

    public bool IsComposerExpanded
    {
        get => _isComposerExpanded;
        private set => SetProperty(ref _isComposerExpanded, value);
    }

    public AsyncRelayCommand LeaveRoomCommand { get; }

    /// <summary>成员栏那个纸飞机按钮:手动加一个人。</summary>
    public RelayCommand AddMemberCommand { get; }

    /// <summary>内容头最右边那个三条横线:切成员栏的显示。</summary>
    public RelayCommand ToggleMemberListCommand { get; }

    /// <summary>频道栏 ⋮ 那个「群组设置」菜单开着没有。</summary>
    public RelayCommand OpenGroupSettingsMenuCommand { get; }

    public RelayCommand CloseGroupSettingsMenuCommand { get; }

    /// <summary>群组设置菜单里的「创建频道 / 创建分类」:先给个默认名字,建完刷新左栏。</summary>
    public RelayCommand CreateChannelCommand { get; }

    public RelayCommand CreateCategoryCommand { get; }

    /// <summary>频道右键菜单(照 Oopz:主页只能邀请/免打扰/编辑/创建,文字频道多复制+删除,语音频道把免打扰换成调度全员到)。</summary>
    public RelayCommand CloseChannelMenuCommand { get; }

    public RelayCommand CopyChannelCommand { get; }

    public RelayCommand DeleteChannelCommand { get; }

    public bool IsChannelMenuOpen
    {
        get => _isChannelMenuOpen;
        private set => SetProperty(ref _isChannelMenuOpen, value);
    }

    /// <summary>主页是虚拟频道,不能复制/删除。</summary>
    public bool ChannelMenuCanCopyOrDelete => _menuChannel is { CategoryId: not HomeCategoryId };

    public bool ChannelMenuIsVoice => _menuChannel?.IsVoice == true;

    /// <summary>右键频道:记住是哪一个,菜单按它决定显示哪几项。</summary>
    public void OpenChannelMenu(RoomChannelViewModel channel)
    {
        _menuChannel = channel;
        IsGroupSettingsMenuOpen = false;
        IsChannelMenuOpen = true;
        OnPropertyChanged(nameof(ChannelMenuCanCopyOrDelete));
        OnPropertyChanged(nameof(ChannelMenuIsVoice));
    }

    public bool IsMemberListVisible
    {
        get => _isMemberListVisible;
        private set
        {
            if (SetProperty(ref _isMemberListVisible, value))
            {
                OnPropertyChanged(nameof(MemberListToggleTip));
                OnPropertyChanged(nameof(MemberListWidth));
            }
        }
    }

    public string MemberListToggleTip => IsMemberListVisible ? "收起成员列表" : "展开成员列表";

    /// <summary>成员栏宽度:收起时收到 0,靠 XAML 里的 DoubleTransition 滑过去(不是直接消失)。</summary>
    public double MemberListWidth => IsMemberListVisible ? 236 : 0;

    public bool IsGroupSettingsMenuOpen
    {
        get => _isGroupSettingsMenuOpen;
        private set => SetProperty(ref _isGroupSettingsMenuOpen, value);
    }



    private Task ToggleMuteAsync() => _services.VoiceSession.SetMutedAsync(!IsMuted);

    private Task ToggleDeafenAsync() => _services.VoiceSession.SetDeafenedAsync(!IsDeafened);

    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(InputText) || SelectedChannel is not { } channel)
        {
            return;
        }

        await _services.Chat.SendAsync(channel.Id, InputText);
        InputText = string.Empty;
    }

    private async Task LeaveRoomAsync()
    {
        await _services.VoiceSession.LeaveAsync();
        _navigation.GoBack();
    }

    /// <summary>切换频道:先翻选中态,语音频道顺手进房;中栏跟着 SelectedChannel 走。</summary>
    /// <summary>加一条消息:同一人、同一天的话不重复画头像和名字。</summary>
    private void Append(ChatMessage message)
    {
        var previous = Messages.Count > 0 ? Messages[^1].Message : null;
        var sameAuthor = previous is not null && previous.AuthorId == message.AuthorId;
        var sameDay = previous is not null
            && previous.SentAt.ToLocalTime().Date == message.SentAt.ToLocalTime().Date;

        Messages.Add(new RoomMessage(message, showHeader: !sameAuthor || !sameDay, showDate: !sameDay));
    }
    private void SelectChannel(RoomChannelViewModel channel, bool joinVoice = true)
    {
        if (ReferenceEquals(SelectedChannel, channel))
        {
            return;
        }

        if (SelectedChannel is { } previous)
        {
            previous.IsSelected = false;
        }

        channel.IsSelected = true;
        SelectedChannel = channel;

        Messages.Clear();

        foreach (var message in _services.Chat.GetHistory(channel.Id))
        {
            Append(message);
        }
        OnPropertyChanged(nameof(SelectedChannel));
        OnPropertyChanged(nameof(SelectedIsVoice));

        if (joinVoice && channel.IsVoice)
        {
            // 占位实现是同步返回的,这里不等它
            _ = _services.VoiceSession.JoinAsync(channel.Id);
        }
    }

    /// <summary>从库里重读分组、频道和成员;不传 selectionId 时尽量保住当前选中频道。</summary>
    private void Reload(string? selectionId = null)
    {
        var previousId = SelectedChannel?.Id;
        var keepId = selectionId ?? previousId ?? Home.Id;
        var group = _services.Groups.GetAll().FirstOrDefault(item => item.Id == Group.Id);

        Categories.Clear();
        LooseChannels.Clear();

        foreach (var category in group?.Categories ?? Array.Empty<ChannelCategory>())
        {
            Categories.Add(new RoomCategoryViewModel(
                category.Id,
                category.Name,
                category.Channels.Select(channel => CreateChannel(channel, category.Id)).ToList()));
        }

        foreach (var channel in group?.LooseChannels ?? Array.Empty<Channel>())
        {
            LooseChannels.Add(CreateChannel(channel, null));
        }

        Members.Clear();
        foreach (var name in group?.MemberNames ?? Array.Empty<string>())
        {
            Members.Add(new RoomMember
            {
                // 还没有真实用户体系,名字先当 id 用
                Profile = new UserProfile { Id = name, DisplayName = name, Status = PresenceStatus.Online },
            });
        }

        ApplyMemberFilter();

        var target = FindChannel(keepId) ?? Home;
        SelectChannel(target, joinVoice: target.Id != previousId);
    }

    /// <summary>收起搜索框:顺手把过滤清掉,成员列表恢复全量。</summary>
    private void CloseMemberSearch()
    {
        IsMemberSearchOpen = false;
        MemberSearchText = string.Empty;
    }

    private void ApplyMemberFilter()
    {
        var keyword = MemberSearchText.Trim();

        VisibleMembers.Clear();

        foreach (var member in Members)
        {
            if (keyword.Length == 0 || member.Profile.DisplayName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                VisibleMembers.Add(member);
            }
        }

        OnPropertyChanged(nameof(MemberGroupLabel));
    }

    private RoomChannelViewModel CreateChannel(Channel channel, string? categoryId) => new(
        channel.Id,
        channel.Name,
        categoryId,
        isVoice: channel.Kind == ChannelKind.Voice,
        capacityText: channel.Kind == ChannelKind.Voice ? channel.CapacityText : null,
        channel => SelectChannel(channel));

    private RoomChannelViewModel? FindChannel(string id) => Categories
        .SelectMany(category => category.Channels)
        .Append(Home)
        .Concat(LooseChannels)
        .FirstOrDefault(channel => channel.Id == id);


    private void CreateChannel()
    {
        _services.Groups.AddChannel(Group.Id, null, "新频道", ChannelKind.Text);
        IsGroupSettingsMenuOpen = false;
        Reload();
    }

    private void CreateCategory()
    {
        _services.Groups.AddCategory(Group.Id, "新分组");
        IsGroupSettingsMenuOpen = false;
        Reload();
    }

    /// <summary>复制频道:同一个分组里再建一个同名频道(本地没有真正的"频道副本"概念)。</summary>
    private void CopyChannel()
    {
        if (_menuChannel is not { CategoryId: not HomeCategoryId } channel)
        {
            return;
        }

        _services.Groups.AddChannel(Group.Id, channel.CategoryId, channel.Name, channel.IsVoice ? ChannelKind.Voice : ChannelKind.Text);
        IsChannelMenuOpen = false;
        Reload();
    }

    // shortcut: 删除频道没走确认弹窗(项目里还没有通用确认框),直接删本地那一行;要做确认框时再加。
    private void DeleteChannel()
    {
        if (_menuChannel is not { CategoryId: not HomeCategoryId } channel)
        {
            return;
        }

        _services.Groups.DeleteChannel(channel.Id);
        IsChannelMenuOpen = false;
        Reload();
    }

    /// <summary>临时:成员靠纸飞机按钮手动加,名字按顺序编号。</summary>
    private void AddMember()
    {
        _services.Groups.AddMember(Group.Id, $"用户 {Members.Count + 1}");
        Reload();
    }
}

/// <summary>左栏频道列表里的一行。主页 / 文字频道 / 语音频道共用同一种项。</summary>
public sealed class RoomChannelViewModel : ViewModelBase
{
    private bool _isSelected;

    public RoomChannelViewModel(
        string id,
        string name,
        string? categoryId,
        bool isVoice,
        string? capacityText,
        Action<RoomChannelViewModel> select)
    {
        Id = id;
        Name = name;
        CategoryId = categoryId;
        IsVoice = isVoice;
        CapacityText = capacityText;
        SelectCommand = new RelayCommand(() => select(this));
    }

    public string Id { get; }

    public string Name { get; }

    /// <summary>所属分组 id。切换频道时只有同一分组(或都没有分组)才做滑动动画。</summary>
    public string? CategoryId { get; }

    public bool IsVoice { get; }

    /// <summary>语音频道右侧的容量文字,主页和文字频道没有。</summary>
    public string? CapacityText { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public RelayCommand SelectCommand { get; }
}

/// <summary>频道列表里的一个分组:标题 + 组内频道。</summary>
public sealed class RoomCategoryViewModel
{
    public RoomCategoryViewModel(string id, string name, IReadOnlyList<RoomChannelViewModel> channels)
    {
        Id = id;
        Name = name;
        Channels = channels;
    }

    public string Id { get; }

    public string Name { get; }

    public IReadOnlyList<RoomChannelViewModel> Channels { get; }

}

/// <summary>
/// 房间消息行。同一人连续发言时,后面几条不再重复头像/名字/时间,直接跟在上面那条下面(照 Oopz)。
/// </summary>
public sealed class RoomMessage
{
    public RoomMessage(ChatMessage message, bool showHeader, bool showDate)
    {
        Message = message;
        ShowHeader = showHeader;
        ShowDate = showDate;
    }

    public ChatMessage Message { get; }

    /// <summary>这一行要不要画头像 + 名字 + 时间(换人或换天时为 true)。</summary>
    public bool ShowHeader { get; }

    /// <summary>这一行上面要不要显示日期分组(每天第一条为 true)。</summary>
    public bool ShowDate { get; }

    public string AuthorName => Message.AuthorName;

    public string Content => Message.Content;

    public string Initial => Message.AuthorName.Length > 0 ? Message.AuthorName[..1] : "?";

    public string DateText => Message.SentAt.ToLocalTime().ToString("yyyy年M月d日");

    public string TimeText => Message.SentAt.ToLocalTime().ToString("M月d日 HH:mm");
}
