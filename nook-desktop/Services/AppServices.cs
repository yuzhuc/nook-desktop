using System;

namespace nook_desktop.Services;

/// <summary>
/// 应用服务集合,同时充当组合根。
///
/// 依赖只在这里装配一次,然后通过构造函数注入到各个 ViewModel。
/// 之所以不用全局静态入口:静态状态会让依赖藏在实现里、无法替换、
/// 也没法在测试里隔离。显式传入的依赖一眼能看清谁依赖了什么。
/// </summary>
public sealed class AppServices
{
    public AppServices(
        IAudioDeviceService audioDevice,
        IVoiceSessionService voiceSession,
        IChatService chat,
        ISettingsService settings,
        IGroupStore groups,
        AppConfigStore config)
    {
        AudioDevice = audioDevice ?? throw new ArgumentNullException(nameof(audioDevice));
        VoiceSession = voiceSession ?? throw new ArgumentNullException(nameof(voiceSession));
        Chat = chat ?? throw new ArgumentNullException(nameof(chat));
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        Groups = groups ?? throw new ArgumentNullException(nameof(groups));
        Config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public IAudioDeviceService AudioDevice { get; }

    public IVoiceSessionService VoiceSession { get; }

    public IChatService Chat { get; }

    public ISettingsService Settings { get; }

    public IGroupStore Groups { get; }

    public AppConfigStore Config { get; }

    /// <summary>
    /// 默认装配:群组走 SQLite 本地库,其余仍是本地占位实现。
    /// 接入真实后端时,只需要替换这里的构造参数,ViewModel 层不用动。
    /// </summary>
    public static AppServices CreateDefault()
    {
        // 聊天消息和群组/频道共用同一个 SQLite 库,所以先建库再注入
        var groups = new SqliteGroupStore();

        return new(
            new PlaceholderAudioDeviceService(),
            new PlaceholderVoiceSessionService(),
            new SqliteChatService(groups),
            new PlaceholderSettingsService(),
            groups,
            new AppConfigStore());
    }
}