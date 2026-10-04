namespace Sholto.Data;

/// <summary>Inspect mode was switched on or off. State: a late subscriber is told the current mode.</summary>
public readonly record struct InspectModeChanged(bool On) : IStateEvent
{
    public int Slot => 0;
}
