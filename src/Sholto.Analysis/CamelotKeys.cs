namespace Sholto.Analysis;

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
/// </summary>
public static class CamelotKeys
{
    // Indexed by pitch class (0=C, 1=C#, …, 11=B). Major = "B" ring.
    private static readonly int[] MajorCamelotNumber = { 8, 3, 10, 5, 12, 7, 2, 9, 4, 11, 6, 1 };
    // Minor = "A" ring.
    private static readonly int[] MinorCamelotNumber = { 5, 12, 7, 2, 9, 4, 11, 6, 1, 8, 3, 10 };

    /// <summary>Format the Camelot code for a key, e.g. (pitchClass=0, isMajor=true) → "8B".</summary>
    public static string ToCamelot(int pitchClass, bool isMajor)
    {
        int n = (isMajor ? MajorCamelotNumber : MinorCamelotNumber)[((pitchClass % 12) + 12) % 12];
        return $"{n}{(isMajor ? "B" : "A")}";
    }

    /// <summary>
    /// Distinct, high-contrast colour for each Camelot key — same convention DJ
    /// software uses: hue rotates around the wheel (red → orange → yellow → green
    /// → cyan → blue → magenta) so adjacent numbers read as adjacent on the
    /// rainbow. Returns 0xRRGGBB; callers wrap in their Avalonia / Skia brush.
    /// </summary>
    public static uint Rgb(string camelot) =>
        Rgb(camelot, hueOffset: 0, saturation: 0.78, majorLightness: 0.55, minorLightness: 0.42);

    /// <summary>Theme-tunable variant. Hue rotates around a 12-position wheel
    /// (one per Camelot number) starting from amber at 8B/8A; <paramref name="hueOffset"/>
    /// rotates that start point (positive = warmer/redder, negative = cooler/bluer).
    /// Major keys use <paramref name="majorLightness"/>; minor keys use the lower
    /// <paramref name="minorLightness"/> so relative pairs read as "same hue,
    /// different mood".</summary>
    public static uint Rgb(string camelot,
                           double hueOffset,
                           double saturation,
                           double majorLightness,
                           double minorLightness)
    {
        if (!TryParse(camelot, out int n, out bool isMajor)) return 0xFFFFFF;
        double hue = (((n - 8) * 30.0 + hueOffset) % 360.0 + 360.0) % 360.0;
        double light = isMajor ? majorLightness : minorLightness;
        return HslToRgb(hue, saturation, light);
    }

    private static uint HslToRgb(double h, double s, double l)
    {
        double c = (1 - Math.Abs(2 * l - 1)) * s;
        double hp = h / 60.0;
        double x = c * (1 - Math.Abs(hp % 2 - 1));
        double r1 = 0, g1 = 0, b1 = 0;
        if      (hp < 1) { r1 = c; g1 = x; }
        else if (hp < 2) { r1 = x; g1 = c; }
        else if (hp < 3) { g1 = c; b1 = x; }
        else if (hp < 4) { g1 = x; b1 = c; }
        else if (hp < 5) { r1 = x; b1 = c; }
        else             { r1 = c; b1 = x; }
        double m = l - c / 2;
        byte r = (byte)Math.Clamp((int)Math.Round((r1 + m) * 255), 0, 255);
        byte g = (byte)Math.Clamp((int)Math.Round((g1 + m) * 255), 0, 255);
        byte b = (byte)Math.Clamp((int)Math.Round((b1 + m) * 255), 0, 255);
        return (uint)((r << 16) | (g << 8) | b);
    }

    /// <summary>Parse a Camelot code like "8B" / "11A" into (number, isMajor). Returns false if malformed.</summary>
    public static bool TryParse(string code, out int number, out bool isMajor)
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

    /// <summary>
    /// Enumerate every Camelot code compatible with <paramref name="from"/>, grouped
    /// by class. Used by the library list to tint rows: Perfect → bright accent,
    /// Close → softer accent, EnergyBoost → tertiary, Far → no tint.
    /// </summary>
    public static IEnumerable<(string Code, Harmony Class)> CompatiblesOf(string from)
    {
        if (!TryParse(from, out int n, out bool maj)) yield break;

        // Same key + relative (number-mate on the opposite letter) — Perfect.
        yield return (ToCode(n, maj), Harmony.Perfect);
        yield return (ToCode(n, !maj), Harmony.Perfect);
        // ±1 on the ring, same letter — Close (perfect 4th / 5th moves).
        yield return (ToCode(Wrap(n + 1), maj), Harmony.Close);
        yield return (ToCode(Wrap(n - 1), maj), Harmony.Close);
        // ±1 with letter swap — diagonal energy boost.
        yield return (ToCode(Wrap(n + 1), !maj), Harmony.EnergyBoost);
        yield return (ToCode(Wrap(n - 1), !maj), Harmony.EnergyBoost);
    }

    private static int Wrap(int n) => ((n - 1) % 12 + 12) % 12 + 1;
    private static string ToCode(int n, bool isMajor) => $"{n}{(isMajor ? "B" : "A")}";

    public static Harmony Compatibility(string a, string b)
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
