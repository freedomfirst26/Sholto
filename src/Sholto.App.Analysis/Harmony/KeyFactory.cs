namespace Sholto.App.Analysis.Harmony;

/// <summary>
/// Builds <see cref="Key"/> values from their Camelot rendering ("8B", "11A").
/// The Camelot-number to pitch-class lookups are derived at construction by
/// enumerating all 24 keys and reading each one's <see cref="Key.CamelotNumber"/>,
/// so the mapping stays written down in exactly one place (the forward tables in <c>KeyRef.CamelotNumber</c>, which Key delegates to).
/// </summary>
public sealed class KeyFactory : IKeyFactory
{
    private readonly int[] _majorPitchClassByCamelotNumber = new int[13]; // index 0 unused; Camelot numbers are 1-12
    private readonly int[] _minorPitchClassByCamelotNumber = new int[13];

    public KeyFactory()
    {
        for (int pitchClass = 0; pitchClass < 12; pitchClass++)
        {
            _majorPitchClassByCamelotNumber[new Key(pitchClass, true).CamelotNumber] = pitchClass;
            _minorPitchClassByCamelotNumber[new Key(pitchClass, false).CamelotNumber] = pitchClass;
        }
    }

    /// <summary>Parse a Camelot code like "8B" / "11A" into a <see cref="Key"/>. Returns false if malformed.</summary>
    public bool TryFromCamelot(string? code, out Key key)
    {
        key = default;
        if (string.IsNullOrEmpty(code) || code.Length < 2 || code.Length > 3) return false;
        char letter = code[^1];
        if (letter != 'A' && letter != 'B' && letter != 'a' && letter != 'b') return false;
        if (!int.TryParse(code[..^1], out int number) || number < 1 || number > 12) return false;
        bool isMajor = letter is 'B' or 'b';
        int pitchClass = (isMajor ? _majorPitchClassByCamelotNumber : _minorPitchClassByCamelotNumber)[number];
        key = new Key(pitchClass, isMajor);
        return true;
    }
}
