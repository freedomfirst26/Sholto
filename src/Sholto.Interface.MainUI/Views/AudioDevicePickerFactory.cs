using Sholto.Data;

namespace Sholto.Interface.MainUI.Views;

public sealed class AudioDevicePickerFactory : IAudioDevicePickerFactory
{
    public AudioDevicePicker Create(IReadOnlyList<OutputDeviceChoice> devices, string? currentName) =>
        new(devices, currentName);
}
