namespace Sholto.Analysis;

/// <summary>
/// Port for <see cref="CamelotKeys"/>. Constructor-injected into everything that
/// formats a Camelot code, parses one, or judges harmonic compatibility
/// (<c>KeyAnalyzer</c>, <c>TrackRow</c>, <c>DeckViewModel</c>) so a test/Bench
/// harness can substitute a fake instead of the real wheel maths — same
/// substitutability reasoning as <see cref="IWaveformPeakAnalyzer"/>. Display
/// colour for a Camelot key is not a harmonic-keys concern — see
/// <c>Sholto.App.Theming.CamelotPalette.KeyBrush</c>.
/// </summary>
public interface IHarmonicKeys
{
    /// <summary>Format the Camelot code for a key, e.g. (pitchClass=0, isMajor=true) → "8B".</summary>
    string ToCamelot(int pitchClass, bool isMajor);

    /// <summary>Parse a Camelot code like "8B" / "11A" into (number, isMajor). Returns false if malformed.</summary>
    bool TryParse(string code, out int number, out bool isMajor);

    /// <summary>Harmonic compatibility class between two Camelot keys.</summary>
    CamelotKeys.Harmony Compatibility(string a, string b);
}
