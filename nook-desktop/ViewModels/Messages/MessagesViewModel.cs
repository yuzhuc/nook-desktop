using nook_desktop.Core.Mvvm;

namespace nook_desktop.ViewModels.Messages;

public sealed class MessagesViewModel : ViewModelBase
{
    private string _emptyHint = "暂无会话";

    public string EmptyHint
    {
        get => _emptyHint;
        set => SetProperty(ref _emptyHint, value);
    }
}
