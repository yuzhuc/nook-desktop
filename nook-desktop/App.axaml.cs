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

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(services),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}