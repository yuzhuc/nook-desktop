namespace nook_desktop.Core.Mvvm;

/// <summary>
/// ViewModel 基类，预留生命周期钩子，后续接入页面显示/隐藏逻辑时无需改动派生类。
/// </summary>
public abstract class ViewModelBase : ObservableObject
{
    public virtual void OnNavigatedTo()
    {
    }

    public virtual void OnNavigatedFrom()
    {
    }
}
