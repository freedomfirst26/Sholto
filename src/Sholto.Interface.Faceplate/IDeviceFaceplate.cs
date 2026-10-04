using Avalonia.Controls;

namespace Sholto.Interface.Faceplate;

/// <summary>One controller's faceplate: its guide file and its drawing. Implemented in
/// Sholto.Interface.Faceplate.Devices, one per device, so the Faceplate core names no hardware.</summary>
public interface IDeviceFaceplate
{
    /// <summary>Stable key, also the name of the user override file
    /// (<c>{DeviceKey}.guide.json</c>).</summary>
    string DeviceKey { get; }

    /// <summary>Opens the embedded guide JSON. The caller disposes the stream.</summary>
    Stream OpenGuide();

    /// <summary>Builds a fresh drawing of the unit.</summary>
    Control CreateLayout();
}
