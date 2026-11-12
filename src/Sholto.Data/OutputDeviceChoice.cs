namespace Sholto.Data;

/// <summary>One output device the user may pick, as the App offers it.</summary>
/// <param name="Name">The device's name: what a <see cref="ChooseOutputDevice"/> answer names.</param>
/// <param name="IsDefault">The system's default output.</param>
/// <param name="IsController">The connected controller's own sound card: picking it keeps master on the controller.</param>
public sealed record OutputDeviceChoice(string Name, bool IsDefault, bool IsController = false);
