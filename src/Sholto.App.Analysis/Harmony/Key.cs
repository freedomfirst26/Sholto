namespace Sholto.App.Analysis.Harmony;

/// <summary>
/// A musical key: pitch class (0=C, 1=C#, … 11=B) plus mode (major/minor).
/// Camelot notation ("8B", "11A") is a RENDERING of a key, not its identity —
/// see <see cref="ToCamelot"/> / <see cref="KeyFactory.TryFromCamelot"/>. Absence of a
/// key must be represented as <c>Key?</c> at every boundary: unlike a Camelot
/// code, there is no empty-string-shaped sentinel, and <c>default(Key)</c>
/// (PitchClass 0, IsMajor false) is itself a legal key (C minor), so it can't
/// double as "no key".
/// </summary>
public readonly record struct Key(int PitchClass, bool IsMajor)
{
    // Indexed by pitch class (0=C, 1=C#, …, 11=B). Major = "B" ring.
    private static readonly int[] MajorCamelotNumber = { 8, 3, 10, 5, 12, 7, 2, 9, 4, 11, 6, 1 };
    // Minor = "A" ring.
    private static readonly int[] MinorCamelotNumber = { 5, 12, 7, 2, 9, 4, 11, 6, 1, 8, 3, 10 };

    /// <summary>Format this key's Camelot code, e.g. (PitchClass=0, IsMajor=true) → "8B".</summary>
    public string ToCamelot() => $"{CamelotNumber}{(IsMajor ? "B" : "A")}";

    /// <summary>This key's Camelot wheel position, 1-12 (the numeric part of <see cref="ToCamelot"/>).</summary>
    public int CamelotNumber =>
        (IsMajor ? MajorCamelotNumber : MinorCamelotNumber)[((PitchClass % 12) + 12) % 12];

    /// <summary>
    /// Harmonic compatibility class between two keys, judged on the Camelot wheel —
    /// mirroring how Rekordbox / Mixed In Key visualise it:
    ///   Perfect     — same key (also the relative major/minor): seamless mix
    ///   Close       — ±1 step on the wheel, same mode: classic perfect-4th / 5th move
    ///   EnergyBoost — diagonal +7 on the same mode (energy lift)
    ///   Far         — everything else: probably clashes
    /// The relation is symmetric in every branch, so <c>a.Compatibility(b)</c> equals
    /// <c>b.Compatibility(a)</c>.
    /// </summary>
    public HarmonicMatchResult Compatibility(Key other)
    {
        // Compare on Camelot numbers, which is what "adjacent on the wheel" means.
        int na = CamelotNumber;
        int nb = other.CamelotNumber;

        if (na == nb && IsMajor == other.IsMajor) return HarmonicMatchResult.Perfect; // identical
        if (na == nb && IsMajor != other.IsMajor) return HarmonicMatchResult.Perfect; // relative maj/min

        int diff = Math.Abs(na - nb);
        // Distance is around a 12-position ring, so 11 apart actually = 1 step.
        if (diff > 6) diff = 12 - diff;

        if (diff == 1 && IsMajor == other.IsMajor) return HarmonicMatchResult.Close;       // ±1 on same ring
        if (diff == 1 && IsMajor != other.IsMajor) return HarmonicMatchResult.EnergyBoost; // diagonal step
        return HarmonicMatchResult.Far;
    }
}
