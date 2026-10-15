using nook_desktop.Core.Mvvm;
using nook_desktop.Core.Navigation;
using nook_desktop.Services;

namespace nook_desktop.ViewModels.Settings;

public sealed class SettingsViewModel : ViewModelBase
{
    private const string InputVolumeKey = "audio.inputVolume";
    private const string OutputVolumeKey = "audio.outputVolume";

    private readonly INavigationService _navigation;
    private readonly ISettingsService _settings;
    private float _inputVolume;
    private float _outputVolume;
    private string _section = "account";

    public SettingsViewModel(INavigationService navigation, ISettingsService settings)
    {
        _navigation = navigation;
        _settings = settings;

        _inputVolume = _settings.Get(InputVolumeKey, 80f);
        _outputVolume = _settings.Get(OutputVolumeKey, 80f);

        BackCommand = new RelayCommand(() => _navigation.GoBack());
        SelectSectionCommand = new RelayCommand<string>(SelectSection);
    }

    public float InputVolume
    {
        get => _inputVolume;
        set
        {
            if (SetProperty(ref _inputVolume, value))
            {
                _settings.Set(InputVolumeKey, value);
            }
        }
    }

    public float OutputVolume
    {
        get => _outputVolume;
        set
        {
            if (SetProperty(ref _outputVolume, value))
            {
                _settings.Set(OutputVolumeKey, value);
            }
        }
    }

    /// <summary>左边导航当前选中项:账号设置 / 语音设置做完了,其余先摆空状态。</summary>
    public string Section
    {
        get => _section;
        private set
        {
            if (!SetProperty(ref _section, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsAccountSection));
            OnPropertyChanged(nameof(IsVoiceSection));
            OnPropertyChanged(nameof(IsPlaceholderSection));
            OnPropertyChanged(nameof(IsPrivacySection));
            OnPropertyChanged(nameof(IsInviteSection));
            OnPropertyChanged(nameof(IsWalletSection));
            OnPropertyChanged(nameof(IsGiftSection));
            OnPropertyChanged(nameof(IsLevelSection));
        }
    }

    public bool IsAccountSection => Section == "account";

    public bool IsPrivacySection => Section == "privacy";

    public bool IsInviteSection => Section == "invite";

    public bool IsWalletSection => Section == "wallet";

    public bool IsGiftSection => Section == "gift";

    public bool IsLevelSection => Section == "level";

    public bool IsVoiceSection => Section == "voice";

    public bool IsPlaceholderSection =>
        !IsAccountSection && !IsVoiceSection;

    /// <summary>OOPZ 设置页左下角那行版本号。</summary>
    public string VersionText => "v1.0.0";

    public RelayCommand BackCommand { get; }

    public RelayCommand<string> SelectSectionCommand { get; }

    private void SelectSection(string? section) => Section = section ?? "account";
}
