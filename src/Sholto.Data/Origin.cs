namespace Sholto.Data;

/// <summary>Where a command came from: which interface, which control on it, which gesture
/// (press, hold, turn...) produced it, and which deck side of the unit the control is on.
/// Convention: every command exposes an <see cref="Origin"/> (implement <see cref="IHasOrigin"/>)
/// so the App can echo, gate or log it by source.
/// <para><paramref name="Deck"/> is the deck side of the control (0 or 1) on a two-deck interface,
/// <see cref="NoDeck"/> for a global control or an interface with no deck side. It says where the
/// input happened, not which deck a command acts on; a command that acts on a deck has its own
/// <c>Deck</c> parameter.</para></summary>
public readonly record struct Origin(string InterfaceId, string ControlId, string GestureName, int Deck = Origin.NoDeck)
{
    /// <summary>The <see cref="Deck"/> of a global control or an interface with no deck side.</summary>
    public const int NoDeck = -1;
}
