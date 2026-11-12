using Sholto.App.Analysis;
using Sholto.App.Analysis.Analyzers.Beats;
using Sholto.App.Analysis.Processing;
using Sholto.App.Analysis.Reporting;
using Sholto.App.Analysis.ToolBoundary;

namespace Sholto.App.ExternalTools;

/// <summary>
/// Beat / downbeat detection — shells out to Beat This! (CPJKU, MIT code and weights).
/// The required beat tracker; madmom (<see cref="MadmomBeatAnalysisStep"/>) is used only
/// when this one is not installed (the choice is made at the composition root).
///
/// Each run gets a fresh temporary work directory for the tool's output file, deleted
/// when the run ends, whatever the outcome.
/// </summary>
public sealed class BeatThisBeatAnalysisStep(
    IExternalTool tool, IAnalysisReporter reporter, IToolDefinition<DetectedBeats> beatThisTool, string tempRoot)
    : ExternalToolAnalysisStep(tool), IBeatAnalysisStep
{
    /// <summary>Single source of truth for the install command — reused verbatim by the
    /// boot-time system check (<see cref="SystemCheck.ToReported"/>, <c>SholtoStackFactory</c>)
    /// and by the "not found" failure below, so the messages can't drift apart. The CPU
    /// torch wheel keeps the install from being a multi-GB CUDA download.</summary>
    public const string InstallCommand =
        "uv tool install beat-this==1.1.0 --with soundfile --index https://download.pytorch.org/whl/cpu --index-strategy unsafe-best-match";

    private readonly IAnalysisReporter _reporter = reporter;
    private readonly IToolDefinition<DetectedBeats> _beatThisTool = beatThisTool;
    private readonly string _tempRoot = tempRoot;

    public override string StepName => AnalysisSteps.Beats;

    /// <summary>Run Beat This! on the file and return its raw beat/downbeat detections.
    /// A missing tool or a failed run throws, surfaced by the caller.</summary>
    public async Task<DetectedBeats> AnalyzeAsync(string filePath, CancellationToken ct = default)
    {
        if (!IsAvailable)
            throw new InvalidOperationException(
                $"Beat This! (beat_this) not found. Install once: `{InstallCommand}`.");

        var workDir = Path.Combine(_tempRoot, $"sholto-beat-this-{Guid.NewGuid():N}");
        try
        {
            var outcome = await Tool.RunAsync(_beatThisTool, new ToolInput(filePath, workDir), _reporter, ct);
            if (!outcome.IsSuccess)
                throw new InvalidOperationException(outcome.Reason);
            return outcome.Value!;
        }
        finally
        {
            if (Directory.Exists(workDir))
                Directory.Delete(workDir, recursive: true);
        }
    }
}
