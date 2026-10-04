namespace Sholto.App.Analysis.Analyzers.Segments;

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
