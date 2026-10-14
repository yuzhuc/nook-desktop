using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using nook_desktop.Models;

namespace nook_desktop.Services;

// 占位实现尚未接线,事件不会有订阅者;真实实现接入时自动生效。
#pragma warning disable CS0067

// ---------------------------------------------------------------------------
// 本地占位实现:让界面在没有后端的情况下也能完整跑通。
// 接入真实后端时,只要在 AppServices.CreateDefault 里替换对应构造参数即可。
// ---------------------------------------------------------------------------

public sealed class PlaceholderAudioDeviceService : IAudioDeviceService
{
    private static readonly AudioDevice[] NoDevices = Array.Empty<AudioDevice>();

    private readonly Dictionary<string, float> _volumes = new();

    public IReadOnlyList<AudioDevice> InputDevices => NoDevices;

    public IReadOnlyList<AudioDevice> OutputDevices => NoDevices;

    public AudioDevice? ActiveInputDevice => null;

    public AudioDevice? ActiveOutputDevice => null;

    public Task RefreshDevicesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<bool> SetInputDeviceAsync(string deviceId, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task<bool> SetOutputDeviceAsync(string deviceId, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task SetInputVolumeAsync(float volume)
    {
        _volumes["input"] = volume;
        return Task.CompletedTask;
    }

    public Task SetOutputVolumeAsync(float volume)
    {
        _volumes["output"] = volume;
        return Task.CompletedTask;
    }
}

public sealed class PlaceholderVoiceSessionService : IVoiceSessionService
{
    private VoiceConnectionState _state = VoiceConnectionState.Disconnected;
    private string? _currentChannelId;
    private bool _isMuted = true;
    private bool _isDeafened;

    public VoiceConnectionState State => _state;

    public string? CurrentChannelId => _currentChannelId;

    public bool IsMuted => _isMuted;

    public bool IsDeafened => _isDeafened;

    public event EventHandler<VoiceConnectionState>? StateChanged;

    public event EventHandler<bool>? MuteChanged;

    public event EventHandler<bool>? DeafenChanged;

    public event EventHandler<float>? LocalVolumeChanged;

    public Task JoinAsync(string channelId, CancellationToken cancellationToken = default)
    {
        _currentChannelId = channelId;
        SetState(VoiceConnectionState.Connected);
        return Task.CompletedTask;
    }

    public Task LeaveAsync()
    {
        _currentChannelId = null;
        SetState(VoiceConnectionState.Disconnected);
        return Task.CompletedTask;
    }

    public Task SetMutedAsync(bool muted)
    {
        if (_isMuted == muted)
        {
            return Task.CompletedTask;
        }

        _isMuted = muted;
        MuteChanged?.Invoke(this, muted);
        return Task.CompletedTask;
    }

    public Task SetDeafenedAsync(bool deafened)
    {
        if (_isDeafened == deafened)
        {
            return Task.CompletedTask;
        }

        _isDeafened = deafened;
        DeafenChanged?.Invoke(this, deafened);
        return Task.CompletedTask;
    }

    private void SetState(VoiceConnectionState state)
    {
        if (_state == state)
        {
            return;
        }

        _state = state;
        StateChanged?.Invoke(this, state);
    }
}


public sealed class PlaceholderSettingsService : ISettingsService
{
    private readonly Dictionary<string, object?> _values = new();

    public T Get<T>(string key, T fallback)
        => _values.TryGetValue(key, out var value) && value is T typed ? typed : fallback;

    public void Set<T>(string key, T value) => _values[key] = value;
}

#pragma warning restore CS0067
