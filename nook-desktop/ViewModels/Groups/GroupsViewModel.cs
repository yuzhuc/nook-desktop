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

public enum GroupCategory
{
    All,
    Joined,
    Recent,
}

public sealed class GroupsViewModel : ViewModelBase
{
    private readonly INavigationService _navigation;
    private readonly AppServices _services;
    private readonly ObservableCollection<Group> _sourceGroups = new();
    private GroupCategory _category = GroupCategory.All;
    private string _searchText = string.Empty;
    private string _statusText = "未连接";

    public GroupsViewModel(INavigationService navigation, AppServices services)
    {
        _navigation = navigation;
        _services = services;

        Groups = new ObservableCollection<GroupCardViewModel>();
        SeedPlaceholderGroups();
        ApplyFilter();

        JoinGroupCommand = new AsyncRelayCommand<GroupCardViewModel>(JoinGroupAsync);
    }

    public ObservableCollection<GroupCardViewModel> Groups { get; }

    public GroupCategory Category
    {
        get => _category;
        set
        {
            if (!SetProperty(ref _category, value))
            {
                return;
            }

            ApplyFilter();
            OnPropertyChanged(nameof(IsAllSelected));
            OnPropertyChanged(nameof(IsJoinedSelected));
            OnPropertyChanged(nameof(IsRecentSelected));
        }
    }

    // ToggleButton 之间没有内建互斥,改由这三个属性统一驱动 Category
    public bool IsAllSelected
    {
        get => Category == GroupCategory.All;
        set { if (value) { Category = GroupCategory.All; } }
    }

    public bool IsJoinedSelected
    {
        get => Category == GroupCategory.Joined;
        set { if (value) { Category = GroupCategory.Joined; } }
    }

    public bool IsRecentSelected
    {
        get => Category == GroupCategory.Recent;
        set { if (value) { Category = GroupCategory.Recent; } }
    }

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

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public AsyncRelayCommand<GroupCardViewModel> JoinGroupCommand { get; }

    public override void OnNavigatedTo() => StatusText = "已连接";

    private async Task JoinGroupAsync(GroupCardViewModel? card)
    {
        if (card?.Group is not { } group)
        {
            return;
        }

        if (group.VoiceChannels.Count > 0)
        {
            await _services.VoiceSession.JoinAsync(group.VoiceChannels[0].Id);
        }

        _navigation.NavigateTo(new RoomViewModel(_navigation, _services, group));
    }

    private void ApplyFilter()
    {
        var source = _sourceGroups.AsEnumerable();

        source = Category switch
        {
            GroupCategory.Joined => source.Where(g => g.IsJoined),
            GroupCategory.Recent => source.Take(2),
            _ => source,
        };

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var keyword = SearchText.Trim();
            source = source.Where(g =>
                g.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                (g.Description?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        Groups.Clear();
        foreach (var group in source)
        {
            Groups.Add(new GroupCardViewModel(group));
        }
    }

    /// <summary>
    /// 占位数据,仅用于让框架在没有后端时也能走通导航。接入真实数据源后删除。
    /// </summary>
    private void SeedPlaceholderGroups()
    {
        _sourceGroups.Add(new Group
        {
            Id = "g1",
            Name = "开黑小队",
            Description = "日常开黑与闲聊,氛围轻松",
            AccentColor = "#3B9EFF",
            MemberCount = 128,
            OnlineCount = 43,
            IsJoined = true,
            VoiceChannels = new[]
            {
                new VoiceChannel { Id = "c1", Name = "休闲开黑", MemberCount = 18, Capacity = 50 },
                new VoiceChannel { Id = "c2", Name = "排位上分", MemberCount = 9, Capacity = 20 },
            },
            TextChannels = new[]
            {
                new TextChannel { Id = "t1", Name = "大厅" },
                new TextChannel { Id = "t2", Name = "开黑组队" },
            },
        });

        _sourceGroups.Add(new Group
        {
            Id = "g2",
            Name = "游戏开发",
            Description = "项目讨论与技术交流",
            AccentColor = "#3DD68C",
            MemberCount = 64,
            OnlineCount = 12,
            VoiceChannels = new[]
            {
                new VoiceChannel { Id = "c3", Name = "语音会议", MemberCount = 6, Capacity = 30 },
            },
            TextChannels = new[]
            {
                new TextChannel { Id = "t3", Name = "技术讨论" },
            },
        });

        _sourceGroups.Add(new Group
        {
            Id = "g3",
            Name = "音乐房",
            Description = "一起听歌,共享耳机时刻",
            AccentColor = "#F5A623",
            MemberCount = 37,
            OnlineCount = 21,
            VoiceChannels = new[]
            {
                new VoiceChannel { Id = "c4", Name = "一起听", MemberCount = 14, Capacity = 25 },
            },
            TextChannels = Array.Empty<TextChannel>(),
        });

        _sourceGroups.Add(new Group
        {
            Id = "g4",
            Name = "深夜电台",
            Description = "凌晨两点的语音房,进来坐坐",
            AccentColor = "#F2555A",
            MemberCount = 92,
            OnlineCount = 5,
            VoiceChannels = new[]
            {
                new VoiceChannel { Id = "c5", Name = "深夜闲聊", MemberCount = 3, Capacity = 40 },
            },
            TextChannels = new[]
            {
                new TextChannel { Id = "t4", Name = "点歌台" },
            },
        });
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

    public string Description => Group.Description ?? string.Empty;

    public IBrush AccentBrush => BuildBrush(Group.AccentColor);

    public string OnlineText => $"{Group.OnlineCount} 人在线";

    public string MemberText => $"{Group.MemberCount} 位成员";

    public string ChannelText => $"{Group.VoiceChannels.Count} 个语音频道";

    public string ActionText => Group.IsJoined ? "进入群组" : "加入群组";

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