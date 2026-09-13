namespace Sholto.Analysis.Analyzers.Beats;

/// <summary>
/// Port for <see cref="BeatgridFitter"/>. Constructor-injected into
/// <c>BasicAnalyzer.ComputeAsync</c> (via its caller) and <c>DeckBeatgrid</c>
/// so a test/Bench harness can substitute a fake instead of the real
/// constant-spacing grid synthesis / least-squares fit.
/// </summary>
public interface IBeatgridFitter
{
    /// <summary>Fit a constant-spacing <see cref="Beatgrid"/> to raw beat and
    /// downbeat detections — tempo, time signature and phase in one value.
    /// Callers that need arrays materialise the result themselves.</summary>
    Beatgrid SynthesizeFullGrid(
        double bpm, double[] rawBeats, double[] rawDownbeats, double durationSec);

    /// <summary>Least-squares fit a constant-spacing grid through every raw
    /// beat detection, rather than trusting just the reported BPM + first
    /// downbeat.</summary>
    BeatgridFitter.GridFitResult FitGrid(double[] beatTimes, double reportedBpm, double firstDownbeatSec);
}
