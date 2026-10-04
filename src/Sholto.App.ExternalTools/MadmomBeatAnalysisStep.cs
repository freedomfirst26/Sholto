using Sholto.App.Analysis;
using Sholto.App.Analysis.Analyzers.Beats;
using Sholto.App.Analysis.Processing;
using Sholto.App.Analysis.Reporting;
using Sholto.App.Analysis.ToolBoundary;

namespace Sholto.App.ExternalTools;

/// <summary>
/// Beat / downbeat detection — shells out to madmom's DBNDownBeatTracker
/// (RNN + dynamic Bayesian network). Required, no fallback.
///
/// Install once:
///   sudo apt install ffmpeg
///   uv tool install madmom-onnx
/// </summary>
public sealed class MadmomBeatAnalysisStep(IExternalTool tool, IAnalysisReporter reporter, IToolDefinition<DetectedBeats> madmomTool)
    : ExternalToolAnalysisStep(tool), IBeatAnalysisStep
{
    public override string StepName => AnalysisSteps.Beats;

    /// <summary>Single source of truth for the install command — reused verbatim by
    /// the boot-time system check (<c>Sholto.Interface.MainUI.SholtoStackFactory.Build</c>) so the two
    /// messages can't drift apart.</summary>
    public const string InstallCommand = "uv tool install madmom-onnx";

    private readonly IAnalysisReporter _reporter = reporter;

    /// <summary>Run madmom on the file and return its raw beat/downbeat detections.
    /// No reporter — madmom is required and has no UI-visible step of its own; a
    /// failure here throws and is surfaced by the caller.</summary>
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
