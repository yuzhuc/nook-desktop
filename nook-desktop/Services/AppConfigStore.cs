using System;
using System.IO;
using System.Text.Json;

namespace nook_desktop.Services;

/// <summary>应用配置。以后加配置项就在这儿加属性,JSON 里是对应的 camelCase 键。</summary>
public sealed class AppConfig
{
    public double WindowWidth { get; set; } = 972;

    public double WindowHeight { get; set; } = 565;
}

/// <summary>
/// 配置读写:%APPDATA%\nook\settings.json。首次启动写一份默认值(等于窗口最小尺寸),
/// 之后只读,想改启动尺寸直接改这个文件。文件被改坏就退回默认值,不影响启动。
/// </summary>
public sealed class AppConfigStore
{
    private const string FileName = "settings.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public AppConfigStore()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "nook");
        Directory.CreateDirectory(directory);
        ConfigPath = Path.Combine(directory, FileName);

        Current = Load();
    }

    public string ConfigPath { get; }

    public AppConfig Current { get; private set; }

    private AppConfig Load()
    {
        if (!File.Exists(ConfigPath))
        {
            var defaults = new AppConfig();
            Save(defaults);
            return defaults;
        }

        try
        {
            var config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(ConfigPath), Options);
            if (config is { WindowWidth: > 0, WindowHeight: > 0 })
            {
                return config;
            }
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
        }

        // 语法坏了或尺寸不合理(0/负数)都退回默认值,不能让配置把启动搞挂
        return new AppConfig();
    }

    /// <summary>缩放拖拽结束时把尺寸写回一次;拖动过程中不写。</summary>
    public void SaveWindowSize(double width, double height)
    {
        if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0)
        {
            return;
        }

        Current.WindowWidth = width;
        Current.WindowHeight = height;
        Save(Current);
    }

    private void Save(AppConfig config)
    {
        try
        {
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(config, Options));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // 写不进去就只用内存里的默认值
        }
    }
}
