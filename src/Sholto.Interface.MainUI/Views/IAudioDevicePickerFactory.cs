using Sholto.Data;

namespace Sholto.Interface.MainUI.Views;

/// <summary>Builds the output-device picker dialog.</summary>
public interface IAudioDevicePickerFactory
{
    /// <param name="devices">The devices on offer.</param>
    /// <param name="currentName">The saved device, to preselect, or null.</param>
    AudioDevicePicker Create(IReadOnlyList<OutputDeviceChoice> devices, string? currentName);
}
