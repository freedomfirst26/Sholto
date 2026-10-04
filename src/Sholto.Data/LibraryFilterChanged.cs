namespace Sholto.Data;

/// <summary>The active library filter: its label (a tag, or a crate with a package emoji), or null when the whole library shows. State.</summary>
public readonly record struct LibraryFilterChanged(string? Label) : IStateEvent
{
    public int Slot => 0;
}
