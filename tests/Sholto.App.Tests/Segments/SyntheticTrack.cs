using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis.Analyzers.Beats;
using Sholto.App.Analysis.Analyzers.Segments;

namespace Sholto.App.Tests.Segments;

/// <summary>A built fixture: the analysis a deck would hold, plus the ground truth it was built from.</summary>
internal sealed record SyntheticTrack(
    BasicAnalysis Analysis,
    Beatgrid Grid,
    IReadOnlyList<SongSection> Sections,
    PhraseGrid Phrases,
    int TotalBars);
