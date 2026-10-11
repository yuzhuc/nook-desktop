using System;
using System.Threading;
using System.Threading.Tasks;

namespace nook_desktop.Services;

/// <summary>
/// 语音会话（进入频道后的通话）生命周期。
/// </summary>
public interface IVoiceSessionService
{
    VoiceConnectionState State { get; }

    string? CurrentChannelId { get; }

    bool IsMuted { get; }

    bool IsDeafened { get; }

    event EventHandler<VoiceConnectionState>? StateChanged;

    event EventHandler<bool>? MuteChanged;

    event EventHandler<bool>? DeafenChanged;

    /// <summary>本端语音电平，范围 0f~1f，用于驱动 UI 上的说话指示。</summary>
    event EventHandler<float>? LocalVolumeChanged;

    Task JoinAsync(string channelId, CancellationToken cancellationToken = default);

    Task LeaveAsync();

    Task SetMutedAsync(bool muted);

    Task SetDeafenedAsync(bool deafened);
}

public enum VoiceConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting,
    Failed,
}
