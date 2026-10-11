using nook_desktop.Core.Mvvm;

namespace nook_desktop.ViewModels.Friends;

public sealed class FriendsViewModel : ViewModelBase
{
    private string _emptyHint = "暂无好友";

    public string EmptyHint
    {
        get => _emptyHint;
        set => SetProperty(ref _emptyHint, value);
    }
}
