using System.Security.Cryptography;
using System.Text;
using Sholto.App.Analysis.Processing;
using Sholto.App.Analysis.Reporting;
using Sholto.App.Analysis.Stems;
using Sholto.App.ExternalTools;

namespace Sholto.Host;

/// <summary>
/// Builds the <see cref="ExternalToolStack"/> once at startup: reads the environment
/// for tool-path / cache-root overrides, resolves every tool's binary, and wires the
/// beat and stem steps. An instance (injected into <c>SholtoStackFactory</c>) rather
/// than a static, because it touches the world and happens exactly once.
///
/// The run/lookup agreement the stem workspace closure preserves is documented on
/// <see cref="ExternalToolStack"/>; the closure lives in <see cref="Build"/> and the
/// directory-name encoding in <see cref="HashPath"/>, the single place an input path
/// becomes a workspace directory.
/// </summary>
public sealed class ExternalToolStackFactory(
    IExternalToolFinderFactory finderFactory,
    IBeatAnalysisStepFactory beatSteps) : IExternalToolStackFactory
{
    private readonly IExternalToolFinderFactory _finderFactory = finderFactory;
    private readonly IBeatAnalysisStepFactory _beatSteps = beatSteps;

    public ExternalToolStack Build()
    {
        var catalog = new ExternalToolCatalog();
        var toolOptions = new ExternalToolOptionsFactory(catalog).FromEnvironment();
        var toolRunner = new ExternalToolRunner();
        // ToolSet (Sholto.App.ExternalTools) resolves every tool's binary path once.
        // It knows nothing about per-tool output/cache layout any more — that
        // policy lives with whoever needs a deterministic work directory. Demucs
        // is the only tool that does: DemucsWorkspaceFor below is handed to BOTH
        // the analysis step (so a run knows where to write) and the cache (so a
        // lookup knows where to look) — they agree on a location because they
        // share this one function, not because either reaches into the other.
        var tools = new ToolSet(toolOptions, toolRunner, catalog, _finderFactory.Create(toolOptions));
        // The run/lookup agreement this closure exists to preserve (see the
        // ExternalToolStack doc) is exactly why the directory-name encoding lives
        // HERE rather than in a shared helper type: there must be exactly one
        // place that turns an input path into a workspace directory, closed
        // over by both the demucs runner and the presence probe. A second
        // copy of this encoding anywhere else is how the Sep 2026 five-day
        // silent stem loss happened.
        //
        // SHA256 (truncated to 16 hex chars) replaces the old "replace unsafe
        // chars with _" scheme: that scheme collapsed an entire absolute path
        // into a single directory component, which could exceed Linux's
        // 255-byte NAME_MAX for deep library paths, and wasn't actually
        // unique — both ' ' and '_' mapped to '_', so "/Music/My Track.mp3"
        // and "/Music/My_Track.mp3" collided into one stem folder and the
        // second track silently inherited the first's stems. A fixed-length
        // hash of the normalised absolute path has neither problem.
        //
        // Known consequence, accepted: every stem directory already on disk
        // was named by the old encoding, so this orphans the existing cache
        // and demucs re-runs across the library. No migration/fallback here —
        // a dual-path lookup is exactly the run/lookup disagreement above.
        string DemucsWorkspaceFor(string inputPath) =>
            Path.Combine(toolOptions.CacheRoot, "stems", HashPath(inputPath));

        // Stems: the raw step (runs demucs unconditionally) and the cache
        // (pure filesystem query, same DemucsWorkspaceFor) are separate
        // components — see DemucsStemAnalysisStep/DemucsStemPresence. Callers on
        // the "double-click to load" path get the CACHING decorator (cache hit
        // -> instant, miss -> real run); a caller like HydrateStemStateAsync,
        // which must NEVER trigger analysis for the hundreds of rows it checks
        // at startup, gets the bare StemCache instead.
        var demucsCache = new DemucsStemPresence(DemucsWorkspaceFor);
        // Confirmed once, cached; shared by the step (the -d flag) and the stage (overlap decision).
        var stemDevice = new CudaProbingStemDevice(tools.PathOrName(ExternalToolNames.Demucs), TimeSpan.FromSeconds(60));
        var demucsRaw = new DemucsStemAnalysisStep(tools.For(ExternalToolNames.Demucs), DemucsWorkspaceFor, stemDevice);
        var demucs = new CachingStemAnalysisStep(demucsRaw, demucsCache);
        var madmom = _beatSteps.Create(tools.For(ExternalToolNames.Madmom), new NullAnalysisReporter());

        // Names the boot probe ToolSet's constructor already ran (see its own doc)
        // and reports it, rather than leaving the result thrown away after boot.
        var check = tools.Check();

        return new ExternalToolStack(
            tools.PathOrName(ExternalToolNames.Ffmpeg),
            demucs,
            demucsCache,
            stemDevice,
            madmom,
            check);
    }

    /// <summary>
    /// Turns an input path into the fixed-length, collision-free directory-name
    /// encoding used for demucs stem workspaces (see the run/lookup-agreement
    /// comments in <see cref="Build"/>). Internal + InternalsVisibleTo so it can be
    /// unit tested without exposing it as public API.
    /// </summary>
    internal string HashPath(string filePath) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(filePath))))[..16];
}
