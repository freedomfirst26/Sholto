namespace Sholto.App.Analysis.Analyzers.Segments;

/// <summary>Tunable thresholds for <see cref="PhraseSectionAnalyzer"/>. Every default is a
/// first guess from synthetic tracks and eight investigated real ones; they exist as
/// properties so they can be tuned against hand-labelled ground truth. Per-bar band levels
/// are expressed relative to the track's "sustained maximum" (see <see cref="SustainBars"/>),
/// so 1.0 means "as loud as the track's loud parts get".</summary>
public sealed record PhraseSectionOptions
{
    /// <summary>Bars per phrase line. 8 for dance music.</summary>
    public int PhraseBars { get; init; } = 8;

    /// <summary>Rolling window (bars) whose 90th percentile defines the sustained maximum of each band.</summary>
    public int SustainBars { get; init; } = 4;

    /// <summary>A bar whose mean low/mid/high level (relative) is below this is silence; leading silence is trimmed (a fade-out at the end belongs to the outro).</summary>
    public float SilenceLevel { get; init; } = 0.03f;

    /// <summary>Kick: the low band at the start of a beat must be at least this (relative) ...</summary>
    public float KickFloor { get; init; } = 0.15f;

    /// <summary>... and exceed the trough later in the beat by at least this fraction of itself.</summary>
    public float KickDepth { get; init; } = 0.10f;

    /// <summary>Fraction of a beat, from its start, searched for the kick peak.</summary>
    public float KickHeadFraction { get; init; } = 0.2f;

    /// <summary>Bass: low-band level (relative) at which bass starts to count as present (0 below)...</summary>
    public float BassFloor { get; init; } = 0.5f;

    /// <summary>... and the extra level above <see cref="BassFloor"/> at which it counts as fully present.</summary>
    public float BassRamp { get; init; } = 0.2f;

    /// <summary>Bars on each side of a candidate change point compared by the novelty measure.</summary>
    public int NoveltyBars { get; init; } = 8;

    /// <summary>Weight of each feature in the novelty distance: low, mid, high, kick, bass, flux.</summary>
    public IReadOnlyList<float> NoveltyWeights { get; init; } = [1f, 1f, 1f, 1f, 1f, 0.5f];

    /// <summary>Minimum novelty (weighted feature distance) for a bar to be a change point.</summary>
    public float ChangeMinStrength { get; init; } = 0.30f;

    /// <summary>A change point must be the strongest within this many bars either side.</summary>
    public int ChangePeakGapBars { get; init; } = 4;

    /// <summary>Snap radius (bars) to an 8-bar phrase line.</summary>
    public int Snap8Bars { get; init; } = 2;

    /// <summary>Snap radius to a 16-bar line (wider than 8: prefer the stronger line).</summary>
    public int Snap16Bars { get; init; } = 3;

    /// <summary>Snap radius to a 32-bar line.</summary>
    public int Snap32Bars { get; init; } = 4;

    /// <summary>Interior sections shorter than this are merged into their most similar neighbour. The first and last are kept (intro / outro remainder).</summary>
    public int MinSectionBars { get; init; } = 8;

    /// <summary>Drop: low-band level (relative to the sustained maximum) must reach this.</summary>
    public float DropLowLevel { get; init; } = 0.85f;

    /// <summary>Drop: high-band level (relative to the sustained maximum) must reach this. Lower than the low level because real high bands wander more.</summary>
    public float DropHighLevel { get; init; } = 0.75f;

    /// <summary>Drop: minimum share of bars with a kick.</summary>
    public float DropKickMin { get; init; } = 0.6f;

    /// <summary>Drop: minimum bass presence.</summary>
    public float DropBassMin { get; init; } = 0.6f;

    /// <summary>A section whose high band rises by at least <see cref="RisingHigh"/> is a build, never a Drop candidate.</summary>
    public bool RisingExcludesDrop { get; init; } = true;

    /// <summary>A Drop needs a "release" before it: the previous section lacks kick or bass (below <see cref="ReleaseMax"/>) or its high band rises by at least <see cref="RisingHigh"/>.</summary>
    public float ReleaseMax { get; init; } = 0.5f;

    /// <summary>Rise of the high band (relative, last third minus first third of a section) that counts as a build.</summary>
    public float RisingHigh { get; init; } = 0.15f;

    /// <summary>Build: kick share at or above this counts as "kick present".</summary>
    public float BuildKickMin { get; init; } = 0.5f;

    /// <summary>If Drops would cover more than this share of the audible track, it is a flat master and nothing is called a Drop.</summary>
    public float MaxDropShare { get; init; } = 0.7f;

    /// <summary>Songs with no drop: first/last section quieter than this share of the loudest sections is Intro / Outro.</summary>
    public float IntroOutroLevel { get; init; } = 0.8f;

    /// <summary>Songs with no drop: two sections whose features differ by less than this (largest per-feature gap) repeat each other.</summary>
    public float RepeatDistance { get; init; } = 0.2f;
}
