namespace Sholto.Data;

/// <summary>The controller's USB connection came up or went down; the App republishes it as <see cref="DeviceConnectionChanged"/>.</summary>
public readonly record struct ReportDeviceConnection(bool Connected, Origin Origin) : ICommand
{
    /// <summary>Not a deck command.</summary>
    public int Deck => -1;
}
