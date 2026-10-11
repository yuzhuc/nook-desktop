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

    public SettingsViewModel(INavigationService navigation, ISettingsService settings)
    {
        _navigation = navigation;
        _settings = settings;

        _inputVolume = _settings.Get(InputVolumeKey, 80f);
        _outputVolume = _settings.Get(OutputVolumeKey, 80f);

        BackCommand = new RelayCommand(() => _navigation.GoBack());
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

    public RelayCommand BackCommand { get; }
}