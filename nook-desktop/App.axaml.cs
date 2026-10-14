using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using nook_desktop.Services;
using nook_desktop.ViewModels;

namespace nook_desktop;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // 组合根:服务在这里装配一次,再通过构造函数逐层注入。
            var services = AppServices.CreateDefault();

            // 启动尺寸来自 %APPDATA%\nook\settings.json,默认就是窗口最小尺寸。
            var config = services.Config.Current;

            var window = new MainWindow
            {
                DataContext = new MainWindowViewModel(services),
                Width = config.WindowWidth,
                Height = config.WindowHeight,
            };

            // 一次缩放拖拽结束时写回一次,拖动过程中不写;下次启动按这个尺寸开。
            window.ResizeCompleted += (_, _) => services.Config.SaveWindowSize(window.Width, window.Height);

            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}