namespace Sholto.Data;

/// <summary>The saved music folder that could not be reached, or null when it can (or none is saved). State.</summary>
public readonly record struct LibraryUnreachableChanged(string? Path) : IStateEvent
{
    public int Slot => 0;
}
