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

public abstract class Channel
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public string? Topic { get; init; }
}

public sealed class VoiceChannel : Channel
{
    public int MemberCount { get; init; }

    public int Capacity { get; init; }
}

public sealed class TextChannel : Channel
{
    public bool IsPrivate { get; init; }
}

public sealed class VoiceRoom
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public string? IconUrl { get; init; }

    public string? Description { get; init; }

    public IReadOnlyList<VoiceChannel> VoiceChannels { get; init; } = System.Array.Empty<VoiceChannel>();

    public IReadOnlyList<TextChannel> TextChannels { get; init; } = System.Array.Empty<TextChannel>();
}

/// <summary>
/// 群组：开屏展示的实体，进入后包含若干语音与文字频道。
/// </summary>
public sealed class Group
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    /// <summary>卡片主色，十六进制字符串，由转换器转成画刷。</summary>
    public string AccentColor { get; init; } = "#3B9EFF";

    public int MemberCount { get; init; }

    public int OnlineCount { get; init; }

    public bool IsJoined { get; init; }

    public IReadOnlyList<VoiceChannel> VoiceChannels { get; init; } = Array.Empty<VoiceChannel>();

    public IReadOnlyList<TextChannel> TextChannels { get; init; } = Array.Empty<TextChannel>();
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
