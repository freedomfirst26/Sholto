using Sholto.App.Analysis;
using Sholto.App.Analysis.Analyzers.Beats;
using Sholto.App.Analysis.Processing;
using Sholto.App.Analysis.Reporting;
using Sholto.App.Analysis.ToolBoundary;

namespace Sholto.App.ExternalTools;

/// <summary>
/// Beat / downbeat detection — shells out to madmom's DBNDownBeatTracker
/// (RNN + dynamic Bayesian network). The optional fallback: used only when Beat This!
/// (<see cref="BeatThisBeatAnalysisStep"/>) is not installed. madmom's models are
/// licensed for non-commercial use only, so it is no longer installed by default.
/// </summary>
public sealed class MadmomBeatAnalysisStep(IExternalTool tool, IAnalysisReporter reporter, IToolDefinition<DetectedBeats> madmomTool)
    : ExternalToolAnalysisStep(tool), IBeatAnalysisStep
{
    public override string StepName => AnalysisSteps.Beats;

    /// <summary>Single source of truth for madmom's install command — shown by the
    /// boot-time system report (<see cref="SystemCheck.ToReported"/>) for the optional
    /// fallback.</summary>
    public const string InstallCommand = "uv tool install madmom-onnx";

    private readonly IAnalysisReporter _reporter = reporter;

    /// <summary>Run madmom on the file and return its raw beat/downbeat detections.
    /// A failure here throws and is surfaced by the caller.</summary>
    public async Task<DetectedBeats> AnalyzeAsync(
        string filePath, CancellationToken ct = default)
    {
        if (!IsAvailable)
            throw new InvalidOperationException(
                $"madmom DBNDownBeatTracker not found. Install once: `{InstallCommand}`.");

        // Madmom has no cached output of its own — its "workspace" is just the
        // source file's own directory, which always already exists.
        var workDir = Path.GetDirectoryName(filePath) ?? "";
        var outcome = await Tool.RunAsync(
            madmomTool, new ToolInput(filePath, workDir), reporter: _reporter, ct);

        if (!outcome.IsSuccess)
            throw new InvalidOperationException(outcome.Reason);

        return outcome.Value!;
    }
}
