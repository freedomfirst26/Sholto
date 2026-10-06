namespace Sholto.App.Decks;

/// <summary>One deck's tune editor and two-point grid-edit mode: whether each is open, and the clicks that
/// set the grid. Raises <see cref="Changed"/> with <see cref="DeckChange.GridEdit"/> or
/// <see cref="DeckChange.EditOpen"/>.</summary>
public interface IDeckTuneEditor
{
    event Action<DeckChange>? Changed;

    bool GridEditActive { get; }

    /// <summary>Toggle two-point grid-edit mode. Entering clears any half-set anchor; exiting discards a
    /// pending first click.</summary>
    void ToggleGridEdit();

    void OnGridClick(double seconds);

    bool EditOpen { get; }

    void ToggleEdit();
    void OpenEdit();
    void CloseEdit();
}
