using Sholto.Analysis;
using Sholto.ExternalTools;

namespace Sholto.App;

/// <summary>
/// The arm's-length external tools (madmom, demucs, ffmpeg), composed once at
/// startup and handed down as a single bundle. Moved out of App.axaml.cs so the
/// composition root's tool-resolution step isn't tangled with the rest of
/// OnFrameworkInitializationCompleted; App just calls <see cref="FromEnvironment"/>
/// and reads through the result.
///
/// Platform is known at boot: build the options once, then hand them to
/// <see cref="ToolSet"/>, which is the only place an <c>ExternalToolFinder</c> is
/// constructed — every tool's path is resolved exactly once, there, and nothing
/// outside this type ever holds the finder itself, only the resolved values
/// <see cref="ToolSet"/> hands out (<c>PathOrName</c> for a plain string, <c>For</c>
/// for a configured <c>IExternalTool</c>).
///
/// <see cref="DemucsWorkspaceFor"/>, the local function below, travels with the
/// stems built here rather than living separately: it is handed to BOTH the
/// analysis step (so a run knows where to write) and the cache (so a lookup
/// knows where to look) — they agree on a location because they share this one
/// closure, not because either reaches into the other. Splitting them apart
/// caused five days of silent stem loss (Sep 2026): a run and a lookup that
/// compute the workspace path differently simply never see each other's output.
///
/// It is NOT exposed as a record component: nothing outside <see cref="FromEnvironment"/>
/// ever reads it (verified during the trunk hoist, 2026-09-12) — both consumers
/// close over the local instead. That closure is what enforces the invariant
/// above; it is stronger than an unread public property would have been, since
/// there is no way for a caller to reach for the function AND compute its own
/// competing path.
/// </summary>
public sealed record ExternalToolStack(
    string FfmpegPath,
    IStemAnalysisStep Stems,
    IStemCache StemCache,
    IBeatAnalysisStep Beats)
{
    public static ExternalToolStack FromEnvironment()
    {
        var toolOptions = ExternalToolOptionsFactory.FromEnvironment();
        var toolRunner = new ExternalToolRunner();
        // ToolSet (Sholto.ExternalTools) resolves every tool's binary path once.
        // It knows nothing about per-tool output/cache layout any more — that
        // policy lives with whoever needs a deterministic work directory. Demucs
        // is the only tool that does: DemucsWorkspaceFor below is handed to BOTH
        // the analysis step (so a run knows where to write) and the cache (so a
        // lookup knows where to look) — they agree on a location because they
        // share this one function, not because either reaches into the other.
        var tools = new ToolSet(toolOptions, toolRunner);
        string DemucsWorkspaceFor(string inputPath) =>
            Path.Combine(toolOptions.CacheRoot, "stems", AnalysisCachePaths.DirNameFor(inputPath));

        // Stems: the raw step (runs demucs unconditionally) and the cache
        // (pure filesystem query, same DemucsWorkspaceFor) are separate
        // components — see DemucsStemAnalysisStep/DemucsStemCache. Callers on
        // the "double-click to load" path get the CACHING decorator (cache hit
        // -> instant, miss -> real run); a caller like HydrateStemStateAsync,
        // which must NEVER trigger analysis for the hundreds of rows it checks
        // at startup, gets the bare StemCache instead.
        var demucsCache = new DemucsStemCache(DemucsWorkspaceFor);
        var demucsRaw = new DemucsStemAnalysisStep(tools.For(ExternalToolNames.Demucs), DemucsWorkspaceFor);
        var demucs = new CachingStemAnalysisStep(demucsRaw, demucsCache);
        var madmom = new MadmomBeatAnalysisStep(tools.For(ExternalToolNames.Madmom));

        return new ExternalToolStack(
            tools.PathOrName(ExternalToolNames.Ffmpeg),
            demucs,
            demucsCache,
            madmom);
    }
}
