using Sholto.Analysis.Reporting;
using Sholto.Analysis.Stems;

namespace Sholto.Analysis.Processing;

/// <summary>
/// Stem separation (vocals / drums / bass / other) for a track. Role-named rather
/// than tool-named: the only implementation today is <see cref="DemucsStemAnalysisStep"/>
/// (demucs), but a fake for tests or a different separator should be substitutable
/// through this without the name lying. Optional — callers keep playing the mixed
/// track if this fails or isn't installed.
/// </summary>
public interface IStemAnalysisStep : IAnalysisStep
{
    /// <summary>Produce stems for <paramref name="filePath"/>. Returns immediately
    /// from cache if a previous run already wrote the four WAVs; otherwise runs the
    /// separator.</summary>
    Task<StemPaths> AnalyzeAsync(
        string filePath, IAnalysisReporter reporter, CancellationToken ct = default);
}
