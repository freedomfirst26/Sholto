using Sholto.Analysis;

namespace Sholto.ExternalTools;

/// <summary>
/// Runs <c>demucs</c> on a track to split it into 4 stems (vocals / drums / bass / other),
/// UNCONDITIONALLY — every call runs the separator. Caching is not this type's
/// concern: it moved out to the <see cref="CachingStemAnalysisStep"/> decorator and
/// the <see cref="DemucsStemCache"/> it queries, which is also the honest place a
/// caller asks "is this already done" without triggering a run (see that type's
/// comment). Composed together at the root, this type is never handed to a caller
/// undecorated in the real app.
///
/// Progress is reported through an <see cref="AnalysisReporter"/> using the step name
/// <see cref="AnalysisSteps.Stems"/> — UI code can show a per-track progress chip by listening to
/// the reporter.
///
/// A run is slow (~30–180 s on CPU). The demucs binary must be on <c>PATH</c>
/// (install.sh installs it via uv).
/// </summary>
public sealed class DemucsStemAnalysisStep : ExternalToolAnalysisStep, IStemAnalysisStep
{
    public override string StepName => AnalysisSteps.Stems;

    // Where this track's stems get written — the same function the composition
    // root hands to DemucsStemCache, so a run's output lands exactly where the
    // cache will later look for it. This type owns none of that policy; it only
    // calls the function it was given.
    private readonly Func<string, string> _workspaceFor;

    public DemucsStemAnalysisStep(IExternalTool tool, Func<string, string> workspaceFor) : base(tool)
    {
        _workspaceFor = workspaceFor;
    }

    /// <summary>
    /// Run demucs on <paramref name="filePath"/> and return the resulting stem
    /// paths. Always runs — no cache check. Callers that want cache short-
    /// circuiting go through <see cref="CachingStemAnalysisStep"/> instead.
    /// </summary>
    public async Task<StemPaths> AnalyzeAsync(
        string filePath,
        IAnalysisReporter reporter,
        CancellationToken ct = default)
    {
        var workDir = _workspaceFor(filePath);
        var paths = StemPaths.PathsIn(workDir);

        // DemucsTool gets the expected paths by value (computed just above) rather
        // than a back-reference to this analyser. Tool.RunAsync ensures workDir
        // exists before running — filesystem policy for THAT lives there, not here.
        var outcome = await Tool.RunAsync(new Sholto.ExternalTools.DemucsTool(paths), filePath, workDir, reporter, ct);
        if (!outcome.IsSuccess)
            throw new InvalidOperationException(outcome.Reason);

        return outcome.Value!;
    }
}
