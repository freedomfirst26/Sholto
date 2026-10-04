namespace Sholto.Data;

/// <summary>Flip MASTER CUE.</summary>
public readonly record struct ToggleMasterCue(Origin Origin) : ICommand
{
    /// <summary>Not a deck command.</summary>
    public int Deck => -1;
}
