namespace Sholto.Data;

/// <summary>The answer to <see cref="OutputDeviceNeeded"/>: the name of the device the user picked, or null
/// when they cancelled the picker.</summary>
public readonly record struct ChooseOutputDevice(string? Name, Origin Origin) : ICommand;
