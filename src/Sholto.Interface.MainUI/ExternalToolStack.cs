using Sholto.App.Analysis;
using Sholto.App.Analysis.Processing;
using Sholto.App.Analysis.Stems;
using Sholto.App.ExternalTools;

namespace Sholto.Interface.MainUI;

/// <summary>
/// The arm's-length external tools (madmom, demucs, ffmpeg), composed once at
/// startup and handed down as a single bundle. Moved out of App.axaml.cs so the
/// composition root's tool-resolution step isn't tangled with the rest of
/// OnFrameworkInitializationCompleted; <see cref="ExternalToolStackFactory"/> builds it and App
/// and reads through the result.
///
/// Platform is known at boot: build the options once, then hand them to
/// <see cref="ToolSet"/>, which is the only place an <c>ExternalToolFinder</c> is
/// constructed — every tool's path is resolved exactly once, there, and nothing
/// outside this type ever holds the finder itself, only the resolved values
/// <see cref="ToolSet"/> hands out (<c>PathOrName</c> for a plain string, <c>For</c>
/// for a configured <c>IExternalTool</c>).
///
/// <c>DemucsWorkspaceFor</c>, the local function in the factory, travels with the
/// stems built here rather than living separately: it is handed to BOTH the
/// analysis step (so a run knows where to write) and the cache (so a lookup
/// knows where to look) — they agree on a location because they share this one
/// closure, not because either reaches into the other. Splitting them apart
/// caused five days of silent stem loss (Sep 2026): a run and a lookup that
/// compute the workspace path differently simply never see each other's output.
///
/// It is NOT exposed as a property: nothing outside <see cref="ExternalToolStackFactory.Build"/>
/// ever reads it (verified during the trunk hoist, 2026-09-12) — both consumers
/// close over the local instead. That closure is what enforces the invariant
/// above; it is stronger than an unread public property would have been, since
/// there is no way for a caller to reach for the function AND compute its own
/// competing path.
/// </summary>
public sealed class ExternalToolStack(
    string ffmpegPath,
    IStemAnalysisStep stems,
    IStemPresence stemCache,
    IBeatAnalysisStep beats,
    SystemCheck check)
{
    public string FfmpegPath { get; } = ffmpegPath;
    public IStemAnalysisStep Stems { get; } = stems;
    public IStemPresence StemCache { get; } = stemCache;
    public IBeatAnalysisStep Beats { get; } = beats;
    public SystemCheck Check { get; } = check;
}
