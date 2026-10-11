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
        ISettingsService settings)
    {
        AudioDevice = audioDevice ?? throw new ArgumentNullException(nameof(audioDevice));
        VoiceSession = voiceSession ?? throw new ArgumentNullException(nameof(voiceSession));
        Chat = chat ?? throw new ArgumentNullException(nameof(chat));
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public IAudioDeviceService AudioDevice { get; }

    public IVoiceSessionService VoiceSession { get; }

    public IChatService Chat { get; }

    public ISettingsService Settings { get; }

    /// <summary>
    /// 默认装配:目前全部使用本地占位实现。
    /// 接入真实后端时,只需要替换这里的构造参数,ViewModel 层不用动。
    /// </summary>
    public static AppServices CreateDefault() => new(
        new PlaceholderAudioDeviceService(),
        new PlaceholderVoiceSessionService(),
        new PlaceholderChatService(),
        new PlaceholderSettingsService());
}