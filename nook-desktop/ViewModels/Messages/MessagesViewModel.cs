using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using nook_desktop.Core.Mvvm;
using nook_desktop.Models;
using nook_desktop.Services;

namespace nook_desktop.ViewModels.Messages;

/// <summary>左栏顶部的两个标签页。</summary>
public enum MessageTab
{
    Private,
    Friends,
}

/// <summary>好友标签页里的筛选。</summary>
public enum FriendFilter
{
    Online,
    All,
    Requests,
    Blocked,
}

public sealed class MessagesViewModel : ViewModelBase
{
    private readonly IChatService _chat;
    private Conversation? _selectedConversation;
    private MessageTab _tab = MessageTab.Private;
    private FriendFilter _friendFilter = FriendFilter.Online;
    private string _inputText = string.Empty;

    public MessagesViewModel(IChatService chat)
    {
        _chat = chat;

        _chat.MessageReceived += (_, message) =>
        {
            if (message.ChannelId == SelectedConversation?.Id)
            {
                Messages.Add(message);
            }
        };

        // 占位数据,仅用于让私信列表在没有后端时也能看到内容。接入真实数据源后删除。
        Conversations.Add(new Conversation("system", "系统通知", isSystem: true));

        SelectConversationCommand = new RelayCommand<Conversation>(SelectConversation);
        SendMessageCommand = new AsyncRelayCommand(SendMessageAsync, () => !string.IsNullOrWhiteSpace(InputText));
    }

    public ObservableCollection<Conversation> Conversations { get; } = new();

    public ObservableCollection<ChatMessage> Messages { get; } = new();

    public MessageTab Tab
    {
        get => _tab;
        set
        {
            if (!SetProperty(ref _tab, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsPrivateTab));
            OnPropertyChanged(nameof(IsFriendsTab));
        }
    }

    // ToggleButton 之间没有内建互斥,改由这两个属性统一驱动 Tab
    public bool IsPrivateTab
    {
        get => Tab == MessageTab.Private;
        set { if (value) { Tab = MessageTab.Private; } }
    }

    public bool IsFriendsTab
    {
        get => Tab == MessageTab.Friends;
        set { if (value) { Tab = MessageTab.Friends; } }
    }

    public FriendFilter Filter
    {
        get => _friendFilter;
        set
        {
            if (!SetProperty(ref _friendFilter, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsOnlineSelected));
            OnPropertyChanged(nameof(IsAllSelected));
            OnPropertyChanged(nameof(IsRequestsSelected));
            OnPropertyChanged(nameof(IsBlockedSelected));
            OnPropertyChanged(nameof(FriendEmptyHint));
        }
    }

    public bool IsOnlineSelected
    {
        get => Filter == FriendFilter.Online;
        set { if (value) { Filter = FriendFilter.Online; } }
    }

    public bool IsAllSelected
    {
        get => Filter == FriendFilter.All;
        set { if (value) { Filter = FriendFilter.All; } }
    }

    public bool IsRequestsSelected
    {
        get => Filter == FriendFilter.Requests;
        set { if (value) { Filter = FriendFilter.Requests; } }
    }

    public bool IsBlockedSelected
    {
        get => Filter == FriendFilter.Blocked;
        set { if (value) { Filter = FriendFilter.Blocked; } }
    }

    /// <summary>还没有好友数据,筛选目前只切换这句空状态文案。</summary>
    public string FriendEmptyHint => Filter switch
    {
        FriendFilter.All => "暂无好友",
        FriendFilter.Requests => "暂无好友请求",
        FriendFilter.Blocked => "暂无黑名单",
        _ => "暂无在线好友",
    };

    public bool HasUnread => Conversations.Any(conversation => conversation.UnreadCount > 0);

    public int UnreadCount => Conversations.Sum(conversation => conversation.UnreadCount);

    public Conversation? SelectedConversation
    {
        get => _selectedConversation;
        private set
        {
            if (SetProperty(ref _selectedConversation, value))
            {
                OnPropertyChanged(nameof(HasSelectedConversation));
                OnPropertyChanged(nameof(IsSystemNotice));
            }
        }
    }

    public bool HasSelectedConversation => _selectedConversation is not null;

    /// <summary>系统通知是只读公告流:不接消息输入,渲染成卡片而不是聊天气泡。</summary>
    public bool IsSystemNotice => _selectedConversation?.IsSystem == true;

    // 占位公告,接入真实数据源后删除
    private readonly List<ChatMessage> _notices = new()
    {
        new ChatMessage("n1", "system", "system", "系统通知", "欢迎使用 nook。左栏是频道列表,点一个进去就能开黑。", DateTimeOffset.Now.AddDays(-2)),
        new ChatMessage("n2", "system", "system", "系统通知", "布局已更新:频道竖排在左栏,启动后默认打开消息页。", DateTimeOffset.Now.AddHours(-3)),
    };

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

    public RelayCommand<Conversation> SelectConversationCommand { get; }

    public AsyncRelayCommand SendMessageCommand { get; }

    private void SelectConversation(Conversation? conversation)
    {
        if (conversation is null)
        {
            return;
        }

        if (SelectedConversation is { } previous)
        {
            previous.IsSelected = false;
        }

        conversation.IsSelected = true;

        SelectedConversation = conversation;
        Messages.Clear();

        if (conversation.IsSystem)
        {
            foreach (var notice in _notices)
            {
                Messages.Add(notice);
            }

            return;
        }

        foreach (var message in _chat.GetHistory(conversation.Id))
        {
            Messages.Add(message);
        }
    }

    private async Task SendMessageAsync()
    {
        var conversationId = SelectedConversation?.Id;
        if (conversationId is null)
        {
            return;
        }

        await _chat.SendAsync(conversationId, InputText);
        InputText = string.Empty;
    }
}
