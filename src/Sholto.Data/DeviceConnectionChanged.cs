namespace Sholto.Data;

/// <summary>The controller's USB connection state. State: a late subscriber is told the current state.</summary>
public readonly record struct DeviceConnectionChanged(bool Connected) : IStateEvent
{
    public int Slot => 0;
}
