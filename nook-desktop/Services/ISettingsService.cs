namespace nook_desktop.Services;

public interface ISettingsService
{
    T Get<T>(string key, T fallback);

    void Set<T>(string key, T value);
}
