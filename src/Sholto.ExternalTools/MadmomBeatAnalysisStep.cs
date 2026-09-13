using Sholto.Analysis;
using Sholto.Analysis.Processing;

namespace Sholto.ExternalTools;

/// <summary>
/// Beat / downbeat detection — shells out to madmom's DBNDownBeatTracker
/// (RNN + dynamic Bayesian network). Required, no fallback.
///
/// Install once:
///   sudo apt install ffmpeg
///   uv tool install madmom-onnx
/// </summary>
public sealed class MadmomBeatAnalysisStep : ExternalToolAnalysisStep, IBeatAnalysisStep
{
    public override string StepName => AnalysisSteps.Beats;

    // MadmomTool is a stateless descriptor (its Name/IsRequired/BuildArgs/Verify
    // don't depend on per-call data), so one instance is built here and reused
    // rather than `new`d on every AnalyzeAsync call.
    private readonly MadmomTool _tool = new();

    public MadmomBeatAnalysisStep(IExternalTool tool) : base(tool)
    {
    }

    /// <summary>Run madmom on the file and return its raw beat/downbeat detections.
    /// No reporter — madmom is required and has no UI-visible step of its own; a
    /// failure here throws and is surfaced by the caller.</summary>
    public async Task<DetectedBeats> AnalyzeAsync(
        string filePath, CancellationToken ct = default)
    {
        if (!IsAvailable)
            throw new InvalidOperationException(
                "madmom DBNDownBeatTracker not found. Install once: `uv tool install madmom-onnx`.");

        // Madmom has no cached output of its own — its "workspace" is just the
        // source file's own directory, which always already exists.
        var workDir = Path.GetDirectoryName(filePath) ?? "";
        var outcome = await Tool.RunAsync(
            _tool, new ToolInput(filePath, workDir), reporter: NullAnalysisReporter.Instance, ct);

        if (!outcome.IsSuccess)
            throw new InvalidOperationException(outcome.Reason);

        return outcome.Value!;
    }
}
