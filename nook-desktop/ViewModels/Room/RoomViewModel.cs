using System.Collections.ObjectModel;
using System.Threading.Tasks;
using nook_desktop.Core.Mvvm;
using nook_desktop.Core.Navigation;
using nook_desktop.Models;
using nook_desktop.Services;

namespace nook_desktop.ViewModels.Room;

public sealed class RoomViewModel : ViewModelBase
{
    private readonly INavigationService _navigation;
    private readonly AppServices _services;
    private bool _isMuted;
    private bool _isDeafened;
    private string _inputText = string.Empty;

    public RoomViewModel(INavigationService navigation, AppServices services, Group group)
    {
        _navigation = navigation;
        _services = services;
        Group = group;

        _isMuted = services.VoiceSession.IsMuted;
        _isDeafened = services.VoiceSession.IsDeafened;

        Members = new ObservableCollection<RoomMember>();
        Messages = new ObservableCollection<ChatMessage>();

        ToggleMuteCommand = new AsyncRelayCommand(ToggleMuteAsync, () => IsConnected);
        ToggleDeafenCommand = new AsyncRelayCommand(ToggleDeafenAsync, () => IsConnected);
        SendMessageCommand = new AsyncRelayCommand(SendMessageAsync, () => !string.IsNullOrWhiteSpace(InputText));
        LeaveRoomCommand = new AsyncRelayCommand(LeaveRoomAsync);

        var voiceSession = services.VoiceSession;
        voiceSession.MuteChanged += (_, muted) => IsMuted = muted;
        voiceSession.DeafenChanged += (_, deafened) => IsDeafened = deafened;
        voiceSession.StateChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(IsConnected));
            ToggleMuteCommand.RaiseCanExecuteChanged();
            ToggleDeafenCommand.RaiseCanExecuteChanged();
        };

        SeedPlaceholderMembers();
    }

    public Group Group { get; }

    public ObservableCollection<RoomMember> Members { get; }

    public ObservableCollection<ChatMessage> Messages { get; }

    public bool IsConnected => _services.VoiceSession.State == VoiceConnectionState.Connected;

    public bool IsMuted
    {
        get => _isMuted;
        private set
        {
            if (SetProperty(ref _isMuted, value))
            {
                OnPropertyChanged(nameof(MicLabel));
            }
        }
    }

    public bool IsDeafened
    {
        get => _isDeafened;
        private set
        {
            if (SetProperty(ref _isDeafened, value))
            {
                OnPropertyChanged(nameof(DeafenLabel));
            }
        }
    }

    public string MicLabel => IsMuted ? "取消静音" : "静音";

    public string DeafenLabel => IsDeafened ? "取消静音听" : "静音听";

    public string InputText
    {
        get => _inputText;
        set
        {
            if (SetProperty(ref _inputText, value))
            {
                SendMessageCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public AsyncRelayCommand ToggleMuteCommand { get; }

    public AsyncRelayCommand ToggleDeafenCommand { get; }

    public AsyncRelayCommand SendMessageCommand { get; }

    public AsyncRelayCommand LeaveRoomCommand { get; }

    private Task ToggleMuteAsync() => _services.VoiceSession.SetMutedAsync(!IsMuted);

    private Task ToggleDeafenAsync() => _services.VoiceSession.SetDeafenedAsync(!IsDeafened);

    private async Task SendMessageAsync()
    {
        var channelId = _services.VoiceSession.CurrentChannelId;
        if (channelId is null)
        {
            return;
        }

        await _services.Chat.SendAsync(channelId, InputText);
        InputText = string.Empty;
    }

    private async Task LeaveRoomAsync()
    {
        await _services.VoiceSession.LeaveAsync();
        _navigation.GoBack();
    }

    /// <summary>
    /// 占位数据,仅用于让右侧成员栏在没有后端时也能看到内容。接入真实数据源后删除。
    /// </summary>
    private void SeedPlaceholderMembers()
    {
        void Add(
            string id,
            string name,
            PresenceStatus status,
            string? channelId = null,
            bool muted = false,
            bool deafened = false,
            float volume = 0f)
            => Members.Add(new RoomMember
            {
                Profile = new UserProfile { Id = id, DisplayName = name, Status = status },
                ChannelId = channelId,
                IsMuted = muted,
                IsDeafened = deafened,
                SpeakingVolume = volume,
            });

        Add("m1", "小林", PresenceStatus.InVoice, "c1", volume: 0.6f);
        Add("m2", "阿泽", PresenceStatus.InVoice, "c1", muted: true, volume: 0.2f);
        Add("m3", "柚子", PresenceStatus.InVoice, "c1", volume: 0.85f);
        Add("m4", "老陈", PresenceStatus.Online);
        Add("m5", "Nia", PresenceStatus.Busy);
        Add("m6", "阿布", PresenceStatus.Away);
        Add("m7", "Kiki", PresenceStatus.Offline);
    }
}