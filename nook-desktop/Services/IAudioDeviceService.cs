using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace nook_desktop.Services;

public interface IAudioDeviceService
{
    IReadOnlyList<AudioDevice> InputDevices { get; }

    IReadOnlyList<AudioDevice> OutputDevices { get; }

    AudioDevice? ActiveInputDevice { get; }

    AudioDevice? ActiveOutputDevice { get; }

    Task RefreshDevicesAsync(CancellationToken cancellationToken = default);

    Task<bool> SetInputDeviceAsync(string deviceId, CancellationToken cancellationToken = default);

    Task<bool> SetOutputDeviceAsync(string deviceId, CancellationToken cancellationToken = default);

    Task SetInputVolumeAsync(float volume);

    Task SetOutputVolumeAsync(float volume);
}

public sealed record AudioDevice(string Id, string Name, bool IsDefault);
