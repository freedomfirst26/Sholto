namespace Sholto.Audio;

/// <summary>
/// A deck's beatgrid correction model. madmom's detection is treated as
/// immutable; what the user sees/hears is a small (bpmOverride, offsetSec)
/// correction regenerated on top of it. See <see cref="DeckBeatgrid"/> for
/// the implementation moved out of <c>Deck</c>, and its file header comment
/// for the full model.
/// </summary>
public interface IDeckBeatgrid
{
    /// <summary>True once any nudge has been applied to the current track's
    /// grid.</summary>
    bool IsGridNudged { get; }

    event Action<bool>? GridNudgedChanged;

    /// <summary>Apply a saved/loaded adjustment. Safe to call before or after
    /// detection lands.</summary>
    void ApplyGridAdjustment(double? bpmOverride, double offsetSec);

    /// <summary>Width adjust: change the effective BPM by
    /// <paramref name="deltaBpm"/> and regenerate the grid spacing.</summary>
    void AdjustBpm(double deltaBpm);

    /// <summary>Coarse phase shift: move the whole grid by N beats.</summary>
    void NudgeGrid(int beats);

    /// <summary>Fine phase shift: move the whole grid by
    /// <paramref name="seconds"/> (typically ±10 ms).</summary>
    void NudgeGridFine(double seconds);

    /// <summary>Set the grid from two clicked downbeat points.</summary>
    void SetGridFromTwoPoints(double tA, double tB);

    /// <summary>Reset the grid to madmom's detection.</summary>
    void ResetGrid();
}
