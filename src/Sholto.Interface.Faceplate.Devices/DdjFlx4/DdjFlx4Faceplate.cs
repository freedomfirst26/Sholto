using Avalonia.Controls;

namespace Sholto.Interface.Faceplate.Devices.DdjFlx4;

/// <summary>The Pioneer DDJ-FLX4's faceplate: embedded guide plus the drawing.</summary>
public sealed class DdjFlx4Faceplate : IDeviceFaceplate
{
    private const string GuideResource = "Sholto.Interface.Faceplate.Devices.DdjFlx4.ddj-flx4.guide.json";

    public string DeviceKey => "ddj-flx4";

    public Stream OpenGuide() =>
        typeof(DdjFlx4Faceplate).Assembly.GetManifestResourceStream(GuideResource)
        ?? throw new InvalidOperationException($"Embedded guide '{GuideResource}' is missing from the assembly.");

    public Control CreateLayout() => new DdjFlx4Layout();
}
