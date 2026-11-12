using Sholto.App.Analysis.Analyzers.Segments;

namespace Sholto.App.Analysis.Analyzers.Complement;

/// <summary>Weights and thresholds for <see cref="ComplementaryRanker"/>. The score of a candidate is
/// the weighted mean of four fits (key, tempo, energy, role), each 0..1; a candidate whose key is
/// <see cref="Harmony.HarmonicMatchResult.Far"/> or whose tempo is outside
/// <see cref="TempoWindowPercent"/> scores zero whatever its other fits.</summary>
public sealed record ComplementOptions
{
    /// <summary>Weight of the key fit in the score.</summary>
    public double KeyWeight { get; init; } = 0.35;

    /// <summary>Weight of the tempo fit in the score.</summary>
    public double TempoWeight { get; init; } = 0.25;

    /// <summary>Weight of the energy (loudness) fit in the score.</summary>
    public double EnergyWeight { get; init; } = 0.2;

    /// <summary>Weight of the section-role fit in the score.</summary>
    public double RoleWeight { get; init; } = 0.2;

    /// <summary>Key fit for the same Camelot number (same key or relative major/minor).</summary>
    public double PerfectKeyFit { get; init; } = 1.0;

    /// <summary>Key fit for one step round the Camelot wheel, same mode.</summary>
    public double CloseKeyFit { get; init; } = 0.8;

    /// <summary>Key fit for one step round the wheel with a mode change (the diagonal).</summary>
    public double EnergyBoostKeyFit { get; init; } = 0.6;

    /// <summary>Largest tempo gap, as a percentage of the playing tempo, that can still be mixed.
    /// The tempo fit falls linearly from 1 at no gap to 0 at this gap; beyond it the candidate scores zero.</summary>
    public double TempoWindowPercent { get; init; } = 6.0;

    /// <summary>The ideal loudness step from the playing section to the candidate: slightly louder
    /// keeps the energy rising.</summary>
    public double EnergyLift { get; init; } = 0.05;

    /// <summary>Distance from the ideal loudness step at which the energy fit reaches 0.</summary>
    public double EnergyGap { get; init; } = 0.3;

    /// <summary>Role fit when the candidate's kind is one of the preferred pairings in <see cref="RolePairs"/>.</summary>
    public double PairedRoleFit { get; init; } = 1.0;

    /// <summary>Role fit for any other kind.</summary>
    public double UnpairedRoleFit { get; init; } = 0.3;

    /// <summary>For each playing section kind, the candidate kinds that continue or contrast it well.</summary>
    public IReadOnlyDictionary<SectionKind, IReadOnlyList<SectionKind>> RolePairs { get; init; } =
        new Dictionary<SectionKind, IReadOnlyList<SectionKind>>
        {
            [SectionKind.Intro] = [SectionKind.Intro, SectionKind.Build],
            [SectionKind.Build] = [SectionKind.Build, SectionKind.Drop],
            [SectionKind.Drop] = [SectionKind.Drop, SectionKind.Build],
            [SectionKind.Breakdown] = [SectionKind.Build, SectionKind.Breakdown],
            [SectionKind.Verse] = [SectionKind.Verse, SectionKind.Chorus],
            [SectionKind.Chorus] = [SectionKind.Chorus, SectionKind.Verse],
            [SectionKind.Bridge] = [SectionKind.Chorus, SectionKind.Build],
            [SectionKind.Outro] = [SectionKind.Intro, SectionKind.Build],
            [SectionKind.Section] = [SectionKind.Section],
        };
}
