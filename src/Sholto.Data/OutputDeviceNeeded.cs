namespace Sholto.Data;

/// <summary>The App needs the user to choose the master output device. A fact: not replayed. An interface
/// that can ask shows a picker over <paramref name="Devices"/> and answers with
/// <see cref="ChooseOutputDevice"/> (a null name when cancelled). The controller's own sound card is never
/// in the list.</summary>
/// <param name="Devices">The devices on offer (shared by reference; do not modify).</param>
/// <param name="CurrentName">The saved device, to preselect, or null.</param>
public readonly record struct OutputDeviceNeeded(IReadOnlyList<OutputDeviceChoice> Devices, string? CurrentName) : IEvent;
