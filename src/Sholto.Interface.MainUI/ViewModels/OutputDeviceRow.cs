namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>One line in the output-device picker.</summary>
/// <param name="Name">The device's display name.</param>
/// <param name="IsDefault">The system's default output.</param>
/// <param name="IsCurrent">The device the app is saved to use.</param>
public sealed record OutputDeviceRow(string Name, bool IsDefault, bool IsCurrent);
