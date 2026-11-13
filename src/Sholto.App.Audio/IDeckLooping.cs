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

    /// <summary>The running loop's length in beats on the beat grid: set when a loop engages, halved and
    /// doubled with it, null with no loop or when its length in beats is not known (a doubling cut short
    /// by the end of the track).</summary>
    double? ActiveLoopBeats { get; }

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

    /// <summary>A BEAT LOOP pad of <paramref name="beats"/> beats: with no loop running it engages one as
    /// <see cref="EnableBeatLoopBeats"/>; with a loop of that length running it exits it; with a loop of
    /// another length running it resizes that loop to <paramref name="beats"/> from the same loop-in, by
    /// halving or doubling it. Every pad length is a power of two, so the ratio is too; if the running
    /// length is unknown or the ratio is not a power of two, the loop exits and the new length engages.
    /// <para>Built from the members above so every implementation gets it unchanged.</para></summary>
    void ResizeOrToggleBeatLoopBeats(double beats)
    {
        // A ratio of 2^12 or more is further than any two pad lengths (1/4 to 32 beats) apart.
        const int maxSteps = 12;
        if (!(beats > 0)) return;
        if (ActiveLoop is null) { EnableBeatLoopBeats(beats); return; }

        var current = ActiveLoopBeats;
        if (current == beats) { ExitLoop(); return; }

        var steps = current is > 0 ? Math.Log2(beats / current.Value) : double.NaN;
        var whole = Math.Round(steps);
        if (double.IsNaN(steps) || Math.Abs(whole) >= maxSteps || current!.Value * Math.Pow(2, whole) != beats)
        {
            ExitLoop();
            EnableBeatLoopBeats(beats);
            return;
        }

        for (var i = 0; i < Math.Abs(whole); i++)
        {
            if (whole > 0) DoubleLoop();
            else HalveLoop();
        }
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
