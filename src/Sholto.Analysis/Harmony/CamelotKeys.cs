namespace Sholto.Analysis.Harmony;

/// <summary>
/// Camelot Wheel mapping + harmonic-distance helpers. Camelot notation lays the
/// 24 musical keys out as a clock: 1–12 around the wheel, A for minor (inner
/// ring), B for major (outer ring). Adjacent positions on the wheel are
/// harmonically compatible — the whole point of the system for DJ mixing.
///
///   1A=Abm  1B=B    2A=Ebm  2B=F#   3A=Bbm  3B=Db
///   4A=Fm   4B=Ab   5A=Cm   5B=Eb   6A=Gm   6B=Bb
///   7A=Dm   7B=F    8A=Am   8B=C    9A=Em   9B=G
///   10A=Bm  10B=D   11A=F#m 11B=A   12A=C#m 12B=E
///
/// Default <see cref="IHarmonicKeys"/>. Pure computation — no environment/
/// filesystem/clock dependency — instance so a test/Bench harness can
/// substitute a fake instead of the real wheel maths; it holds no state of
/// its own (the lookup tables below are fixed data, same as MajorProfile/
/// MinorProfile on <c>KeyAnalyzer</c>).
/// </summary>
public sealed class CamelotKeys : IHarmonicKeys
{
    // Indexed by pitch class (0=C, 1=C#, …, 11=B). Major = "B" ring.
    private static readonly int[] MajorCamelotNumber = { 8, 3, 10, 5, 12, 7, 2, 9, 4, 11, 6, 1 };
    // Minor = "A" ring.
    private static readonly int[] MinorCamelotNumber = { 5, 12, 7, 2, 9, 4, 11, 6, 1, 8, 3, 10 };

    /// <summary>Format the Camelot code for a key, e.g. (pitchClass=0, isMajor=true) → "8B".</summary>
    public string ToCamelot(int pitchClass, bool isMajor)
    {
        int n = (isMajor ? MajorCamelotNumber : MinorCamelotNumber)[((pitchClass % 12) + 12) % 12];
        return $"{n}{(isMajor ? "B" : "A")}";
    }

    /// <summary>Parse a Camelot code like "8B" / "11A" into (number, isMajor). Returns false if malformed.</summary>
    public bool TryParse(string code, out int number, out bool isMajor)
    {
        number = 0; isMajor = false;
        if (string.IsNullOrEmpty(code) || code.Length < 2 || code.Length > 3) return false;
        char letter = code[^1];
        if (letter != 'A' && letter != 'B' && letter != 'a' && letter != 'b') return false;
        if (!int.TryParse(code[..^1], out number) || number < 1 || number > 12) return false;
        isMajor = letter is 'B' or 'b';
        return true;
    }

    /// <summary>
    /// Harmonic compatibility class between two Camelot keys, mirroring how
    /// Rekordbox / Mixed In Key visualise the wheel:
    ///   Perfect   — same key (also the relative major/minor — same number,
    ///               opposite letter): seamless mix
    ///   Close     — ±1 step on the wheel, same letter: classic perfect-4th / 5th move
    ///   EnergyBoost — diagonal +7 on the same letter (energy lift)
    ///   Far       — everything else: probably clashes
    /// </summary>
    public enum Harmony { Perfect, Close, EnergyBoost, Far }

    public Harmony Compatibility(string a, string b)
    {
        if (!TryParse(a, out int na, out bool ma) || !TryParse(b, out int nb, out bool mb))
            return Harmony.Far;

        if (na == nb && ma == mb) return Harmony.Perfect;                   // identical
        if (na == nb && ma != mb) return Harmony.Perfect;                   // relative maj/min

        int diff = Math.Abs(na - nb);
        // Distance is around a 12-position ring, so 11 apart actually = 1 step.
        if (diff > 6) diff = 12 - diff;

        if (diff == 1 && ma == mb) return Harmony.Close;                    // ±1 on same ring
        if (diff == 1 && ma != mb) return Harmony.EnergyBoost;              // diagonal step
        return Harmony.Far;
    }
}
