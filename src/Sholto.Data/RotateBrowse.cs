namespace Sholto.Data;

/// <summary>The browse knob turned by <see cref="Delta"/> clicks.</summary>
public readonly record struct RotateBrowse(int Delta, Origin Origin) : ICommand
{
    /// <summary>Not a deck command.</summary>
    public int Deck => -1;
}
