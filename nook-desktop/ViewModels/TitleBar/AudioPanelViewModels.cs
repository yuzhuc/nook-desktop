using System.Collections.ObjectModel;
using System.Threading.Tasks;
using nook_desktop.Core.Mvvm;
using nook_desktop.Services;

namespace nook_desktop.ViewModels.TitleBar;

public sealed class MicPanelViewModel : ViewModelBase
{
    private const string VolumeKey = "audio.inputVolume";

    private readonly ISettingsService _settings;
    private readonly IVoiceSessionService _voiceSession;
    private float _volume;
    private bool _isMuted = true;

    public MicPanelViewModel(
        IAudioDeviceService audioDevice,
        IVoiceSessionService voiceSession,
        ISettingsService settings)
    {
        _settings = settings;
        _voiceSession = voiceSession;

        _isMuted = voiceSession.IsMuted;
        _volume = settings.Get(VolumeKey, 80f);
        Devices = new ObservableCollection<AudioDevice>(audioDevice.InputDevices);
        ToggleMuteCommand = new AsyncRelayCommand(ToggleMuteAsync);
    }

    public string Title => "麦克风";

    public ObservableCollection<AudioDevice> Devices { get; }

    public bool IsMuted
    {
        get => _isMuted;
        private set
        {
            if (SetProperty(ref _isMuted, value))
            {
                OnPropertyChanged(nameof(MuteText));
            }
        }
    }

    public string MuteText => IsMuted ? "取消静音" : "静音";

    public float Volume
    {
        get => _volume;
        set
        {
            if (SetProperty(ref _volume, value))
            {
                OnPropertyChanged(nameof(VolumeText));
                _settings.Set(VolumeKey, value);
            }
        }
    }

    public string VolumeText => $"{Volume:F0}%";

    public AsyncRelayCommand ToggleMuteCommand { get; }

    private async Task ToggleMuteAsync()
    {
        var target = !IsMuted;
        await _voiceSession.SetMutedAsync(target);
        IsMuted = target;
    }
}

public sealed class HeadphonePanelViewModel : ViewModelBase
{
    private const string VolumeKey = "audio.outputVolume";

    private readonly ISettingsService _settings;
    private readonly IVoiceSessionService _voiceSession;
    private float _volume;
    private bool _isDeafened;

    public HeadphonePanelViewModel(
        IAudioDeviceService audioDevice,
        IVoiceSessionService voiceSession,
        ISettingsService settings)
    {
        _settings = settings;
        _voiceSession = voiceSession;

        _isDeafened = voiceSession.IsDeafened;
        _volume = settings.Get(VolumeKey, 80f);
        Devices = new ObservableCollection<AudioDevice>(audioDevice.OutputDevices);
        ToggleDeafenCommand = new AsyncRelayCommand(ToggleDeafenAsync);
    }

    public string Title => "耳机";

    public ObservableCollection<AudioDevice> Devices { get; }

    public bool IsDeafened
    {
        get => _isDeafened;
        private set
        {
            if (SetProperty(ref _isDeafened, value))
            {
                OnPropertyChanged(nameof(DeafenText));
            }
        }
    }

    public string DeafenText => IsDeafened ? "取消静音听" : "静音听";

    public float Volume
    {
        get => _volume;
        set
        {
            if (SetProperty(ref _volume, value))
            {
                OnPropertyChanged(nameof(VolumeText));
                _settings.Set(VolumeKey, value);
            }
        }
    }

    public string VolumeText => $"{Volume:F0}%";

    public AsyncRelayCommand ToggleDeafenCommand { get; }

    private async Task ToggleDeafenAsync()
    {
        var target = !IsDeafened;
        await _voiceSession.SetDeafenedAsync(target);
        IsDeafened = target;
    }
}

public sealed class KeyboardPanelViewModel : ViewModelBase
{
    public KeyboardPanelViewModel(IAudioDeviceService audioDevice)
    {
        Devices = new ObservableCollection<AudioDevice>(audioDevice.InputDevices);
        Shortcuts = new ObservableCollection<ShortcutItem>
        {
            new("F1", "打开设置"),
            new("F2", "切换麦克风静音"),
            new("F3", "切换静音听"),
            new("F4", "离开当前房间"),
        };
    }

    public string Title => "键盘";

    public ObservableCollection<AudioDevice> Devices { get; }

    public ObservableCollection<ShortcutItem> Shortcuts { get; }
}

public sealed record ShortcutItem(string Key, string Description);