namespace Sholto.Data;

/// <summary>Open the Glance search overlay, or toggle its table/rail focus if it is already open. The App relays
/// it as <see cref="SearchRequested"/>; the overlay itself is Interface state.</summary>
public readonly record struct OpenSearch(Origin Origin) : ICommand
{
    /// <summary>Not a deck command.</summary>
    public int Deck => -1;
}
