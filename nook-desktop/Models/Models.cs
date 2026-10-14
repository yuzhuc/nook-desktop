using System;
using System.Collections.Generic;

namespace nook_desktop.Models;

public enum PresenceStatus
{
    Offline,
    Online,
    InVoice,
    Busy,
    Away,
}

/// <summary>用户在客户端内手动设置的状态，显示在头像右下角。</summary>
public enum UserStatus
{
    Online,
    Away,
    Busy,
    Offline,
}

public sealed class UserProfile
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public string? AvatarUrl { get; init; }

    public PresenceStatus Status { get; init; } = PresenceStatus.Offline;
}

public enum ChannelKind
{
    Text,
    Voice,
}

public sealed class Channel
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public ChannelKind Kind { get; init; }

    public int MemberCount { get; init; }

    public int Capacity { get; init; }

    /// <summary>语音频道行右侧的容量文字,形如 00/50;文字频道不用它。</summary>
    public string CapacityText => $"{MemberCount:00}/{Capacity:00}";
}

/// <summary>频道分组。只是频道列表里的一层壳,自己点不进去。</summary>
public sealed class ChannelCategory
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public IReadOnlyList<Channel> Channels { get; init; } = Array.Empty<Channel>();
}

/// <summary>
/// 群组：左侧导航里的实体，进入后是若干频道分组和频道。
/// </summary>
public sealed class Group
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    /// <summary>卡片主色，十六进制字符串，由转换器转成画刷。</summary>
    public string AccentColor { get; init; } = "#3B9EFF";

    /// <summary>置顶的域在域栏最上面单独排一组(最新的置顶排第一)。</summary>
    public bool IsPinned { get; init; }

    public int MemberCount { get; init; }

    public int OnlineCount { get; init; }

    public bool IsJoined { get; init; }

    public IReadOnlyList<ChannelCategory> Categories { get; init; } = Array.Empty<ChannelCategory>();

    /// <summary>没有归到任何分组里的频道。</summary>
    public IReadOnlyList<Channel> LooseChannels { get; init; } = Array.Empty<Channel>();

    /// <summary>手动加进来的成员名。接入真实用户体系前只存名字。</summary>
    public IReadOnlyList<string> MemberNames { get; init; } = Array.Empty<string>();
}

public sealed class RoomMember
{
    public required UserProfile Profile { get; init; }

    public string? ChannelId { get; init; }

    public bool IsMuted { get; init; }

    public bool IsDeafened { get; init; }

    /// <summary>该成员当前语音强度，范围 0f~1f。</summary>
    public float SpeakingVolume { get; init; }
}

public sealed record ChatMessage(string Id, string ChannelId, string AuthorId, string AuthorName, string Content, DateTimeOffset SentAt);

/// <summary>
/// 私信会话。列表行要跟着选中态换底色,所以是可通知的普通类(原来的 record 没法通知)。
/// </summary>
public sealed class Conversation : System.ComponentModel.INotifyPropertyChanged
{
    private bool _isSelected;

    public Conversation(string id, string name, int unreadCount = 0, bool isSystem = false)
    {
        Id = id;
        Name = name;
        UnreadCount = unreadCount;
        IsSystem = isSystem;
    }

    public string Id { get; }

    public string Name { get; }

    public int UnreadCount { get; }

    public bool IsSystem { get; }

    public bool HasUnread => UnreadCount > 0;

    /// <summary>是不是列表里当前打开的那一条。</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
}

