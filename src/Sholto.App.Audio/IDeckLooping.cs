namespace Sholto.App.Audio;

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

    /// <summary>Engage a loop of <paramref name="beats"/> beats, or exit the active loop, as
    /// <see cref="EnableBeatLoop"/>. Whole bars (a multiple of four beats) are <see cref="EnableBeatLoop"/>
    /// itself. A shorter power-of-two length (2, 1, 1/2, 1/4 … beats) engages a one-bar loop and halves it
    /// down to that length, so its loop-in is snapped exactly as a bar loop's is. Any other length is
    /// ignored.
    /// <para>Built from the members above so every implementation gets it unchanged.</para></summary>
    void EnableBeatLoopBeats(double beats)
    {
        const int beatsPerBar = 4;
        // Halving a bar this many times reaches 1/1024 of a beat; nothing a pad asks for is shorter.
        const int maxHalvings = 12;
        if (!(beats > 0)) return;
        if (beats >= beatsPerBar)
        {
            var bars = beats / beatsPerBar;
            if (bars == Math.Floor(bars) && bars <= int.MaxValue) EnableBeatLoop((int)bars);
            return;
        }

        var halvings = 0;
        double length = beatsPerBar;
        while (length > beats && halvings < maxHalvings)
        {
            length /= 2;
            halvings++;
        }
        if (length != beats) return;

        EnableBeatLoop(1);
        // No loop now: either one was running and that press exited it, or the deck could not loop yet.
        if (ActiveLoop is null) return;
        for (var i = 0; i < halvings; i++) HalveLoop();
    }

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
