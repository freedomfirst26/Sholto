namespace Sholto.Audio;

/// <summary>
/// A deck's beat-synced loop: engage/exit, and halve/double its length. The
/// data provider does the actual sample-accurate wrap; this port only computes
/// where the loop in/out points sit (snapped to the beatgrid) and pushes the
/// region in. See <see cref="DeckLooping"/> for the implementation moved out of
/// <c>Deck</c>.
/// </summary>
public interface IDeckLooping
{
    /// <summary>The deck's current loop, or null if none.</summary>
    LoopRegion? ActiveLoop { get; }

    /// <summary>Fires on the thread that mutated the loop (UI / MIDI). Argument
    /// is the new region or null on exit.</summary>
    event Action<LoopRegion?>? LoopChanged;

    /// <summary>Engage an N-bar auto-loop snapped to the nearest downbeat. If a
    /// loop is already active this toggles it off. No-op if the beatgrid hasn't
    /// landed yet, or the deck isn't in stem-mix playback.</summary>
    void EnableBeatLoop(int bars);

    /// <summary>Halve the active loop's length (loop-in stays, loop-out moves).
    /// No-op if not looping.</summary>
    void HalveLoop();

    /// <summary>Double the active loop's length, clamped to track end. No-op if
    /// not looping.</summary>
    void DoubleLoop();

    /// <summary>Exit the active loop; playback continues forward from the
    /// current cursor. No-op if not looping.</summary>
    void ExitLoop();
}
