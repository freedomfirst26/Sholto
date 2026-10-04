namespace Sholto.Data;

/// <summary>One output device the user may pick, as the App offers it.</summary>
public sealed record OutputDeviceChoice(string Name, bool IsDefault);
