namespace Sholto.Interface.MainUI.ViewModels.Glance;

/// <summary>Reads a deck's playhead for the Glance countdown. The deck view models implement it in the window
/// wiring; tests pass values in.</summary>
public interface IDeckClockSource
{
    /// <summary>The current reading for <paramref name="deck"/> (0-based).</summary>
    DeckClockReading Read(int deck);
}
