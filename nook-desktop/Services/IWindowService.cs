namespace nook_desktop.Services;

/// <summary>
/// 窗口控制，屏蔽 ViewModel 对具体 Window 的依赖。
/// </summary>
public interface IWindowService
{
    void Minimize();

    void ToggleMaximize();

    void Close();
}
