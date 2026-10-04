using Sholto.App.Audio;

namespace Sholto.Interface.MainUI.Views;

public sealed class AudioDevicePickerFactory : IAudioDevicePickerFactory
{
    public AudioDevicePicker Create(IReadOnlyList<AudioDevice> devices, string? currentName) =>
        new(devices, currentName);
}
