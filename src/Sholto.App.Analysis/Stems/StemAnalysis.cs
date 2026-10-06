using Sholto.App.Analysis.Analyzers.Vocals;
using Sholto.Data;

namespace Sholto.App.Analysis.Stems;

/// <summary>
/// Outcome of the stem stage: where the stems are, their decoded samples (~370 MB,
/// so consumers hand them on and do not store this record) and the vocal regions.
/// </summary>
public sealed record StemAnalysis(StemPaths Paths, StemSamples Samples, IReadOnlyList<VocalRegion> Vocals);
