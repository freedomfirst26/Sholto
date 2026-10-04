using Sholto.App.Analysis.Analyzers;

namespace Sholto.App.Analysis.Analyzers.Beats;

/// <summary>
/// Port for <see cref="BeatgridAnalyzer"/>. Constructor-injected into
/// <c>BasicAnalysisStage.RunAsync</c> (via its caller)
/// so a test/Bench harness can substitute a fake instead of the real
/// constant-spacing grid synthesis.
/// </summary>
public interface IBeatgridAnalyzer : IAnalyzer
{
    /// <summary>Fit a constant-spacing <see cref="Beatgrid"/> to raw beat and
    /// downbeat detections — tempo, time signature and phase in one value.
    /// Callers that need arrays materialise the result themselves.</summary>
    Beatgrid FromDetections(
        double bpm, double[] rawBeats, double[] rawDownbeats, double durationSec);
}
