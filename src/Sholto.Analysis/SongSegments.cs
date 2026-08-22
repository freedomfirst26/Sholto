namespace Sholto.Analysis;

/// <summary>A coarse structural label for a stretch of a track. Derived from the
/// energy envelope aligned to the beatgrid — honest and cheap, not a trained model,
/// so treat labels as a hint. See <see cref="SongSegmentAnalyzer"/>.</summary>
public enum SegmentKind
{
    Intro,
    BuildUp,
    Drop,
    Breakdown,
    Verse,
    Chorus,
    Bridge,
    Outro,
}

/// <summary>One contiguous section of a track, bar-aligned.</summary>
public readonly record struct SongSegment(double StartSec, double EndSec, SegmentKind Kind, float Energy);

/// <summary>The track's structure: a handful of bar-aligned sections with coarse
/// labels and a 0..1 energy each. Drives the minimap's colouring so intro / build /
/// drop / breakdown / outro read at a glance.</summary>
public sealed record SongSegments(IReadOnlyList<SongSegment> Segments) : IAnalysis
{
    public string Name => "SongSegments";
    public static SongSegments Empty { get; } = new(System.Array.Empty<SongSegment>());
}
