namespace Sholto.Data;

/// <summary>A musical key as it crosses the bus: pitch class (0 = C ... 11 = B) and mode. Camelot
/// notation ("8B", "11A") is a rendering of the key, not its identity — see <see cref="ToCamelot"/>.</summary>
public readonly record struct KeyRef(int PitchClass, bool IsMajor)
{
    /// <summary>This key's Camelot wheel position, 1-12 (the numeric part of <see cref="ToCamelot"/>).</summary>
    public int CamelotNumber
    {
        get
        {
            int pc = ((PitchClass % 12) + 12) % 12;
            return IsMajor
                ? pc switch { 0 => 8, 1 => 3, 2 => 10, 3 => 5, 4 => 12, 5 => 7, 6 => 2, 7 => 9, 8 => 4, 9 => 11, 10 => 6, _ => 1 }
                : pc switch { 0 => 5, 1 => 12, 2 => 7, 3 => 2, 4 => 9, 5 => 4, 6 => 11, 7 => 6, 8 => 1, 9 => 8, 10 => 3, _ => 10 };
        }
    }

    /// <summary>Format this key's Camelot code, e.g. (PitchClass=0, IsMajor=true) → "8B".</summary>
    public string ToCamelot() => $"{CamelotNumber}{(IsMajor ? "B" : "A")}";
}
