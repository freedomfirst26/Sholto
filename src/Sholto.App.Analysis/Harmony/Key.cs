using Sholto.Data;

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
    /// <summary>Format this key's Camelot code, e.g. (PitchClass=0, IsMajor=true) → "8B".</summary>
    public string ToCamelot() => new KeyRef(PitchClass, IsMajor).ToCamelot();

    /// <summary>This key's Camelot wheel position, 1-12 (the numeric part of <see cref="ToCamelot"/>).</summary>
    public int CamelotNumber => new KeyRef(PitchClass, IsMajor).CamelotNumber;

    /// <summary>
    /// Harmonic compatibility class between two keys, judged on the Camelot wheel —
    /// mirroring how Rekordbox / Mixed In Key visualise it. See <see cref="HarmonicMatchResult"/>.
    /// The relation is symmetric in every branch, so <c>a.Compatibility(b)</c> equals
    /// <c>b.Compatibility(a)</c>.
    /// </summary>
    public HarmonicMatchResult Compatibility(Key other)
    {
        // Compare on Camelot numbers, which is what "adjacent on the wheel" means.
        int na = CamelotNumber;
        int nb = other.CamelotNumber;

        if (na == nb) return HarmonicMatchResult.Perfect; // identical, or relative maj/min

        int diff = Math.Abs(na - nb);
        // Distance is around a 12-position ring, so 11 apart actually = 1 step.
        if (diff > 6) diff = 12 - diff;

        if (diff == 1) return IsMajor == other.IsMajor ? HarmonicMatchResult.Close : HarmonicMatchResult.EnergyBoost;
        return HarmonicMatchResult.Far;
    }

    /// <summary>Every key that mixes with this one: all 24 keys whose <see cref="Compatibility"/> is not
    /// <see cref="HarmonicMatchResult.Far"/> (this key included). For 8B that is 8B, 8A, 7B, 9B, 7A, 9A.
    /// Note: the Glance FitScorer scores the diagonal (e.g. 8B to 9A) as 0 while this set, and so the
    /// library's eligibility outline, includes it.</summary>
    public IReadOnlyList<Key> MixableKeys()
    {
        var mixable = new List<Key>();
        for (int pitchClass = 0; pitchClass < 12; pitchClass++)
        {
            foreach (var isMajor in new[] { true, false })
            {
                var candidate = new Key(pitchClass, isMajor);
                if (Compatibility(candidate) != HarmonicMatchResult.Far) mixable.Add(candidate);
            }
        }
        return mixable;
    }
}
