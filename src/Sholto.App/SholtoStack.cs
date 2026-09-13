using Sholto.Analysis;
using Sholto.Audio;
using Sholto.ExternalTools;
using Sholto.Library;
using Sholto.Storage;

namespace Sholto.App;

/// <summary>
/// The plain-.NET leaves of the composition root, built ONCE in
/// <c>Program.BuildAvaloniaApp</c>'s <c>AppBuilder.Configure(() => ...)</c> factory —
/// BEFORE <c>App.Initialize()</c> and <c>App.OnFrameworkInitializationCompleted</c> run.
///
/// <para><b>Why here, and why now.</b> Avalonia's own docs say nothing running inside
/// that factory lambda may touch Avalonia types or expect a <c>SynchronizationContext</c>
/// — it runs ahead of <c>StartWithClassicDesktopLifetime</c>'s own setup. Every leaf
/// bundled here was checked against that rule during the trunk hoist (2026-09-12): none
/// constructs a window, a <c>Dispatcher</c>/<c>DispatcherTimer</c>, or touches Avalonia
/// styles/themes/resources. <see cref="ThemeContext"/> is the one leaf that does NOT
/// belong here — <see cref="Theming.SholtoThemeJson.LoadAll"/> resolves bundled themes
/// through Avalonia's <c>AssetLoader</c> (<c>avares://</c>), which needs a live
/// application — so it stays a field on <c>App</c>, built in
/// <c>OnFrameworkInitializationCompleted</c> exactly as before.</para>
///
/// <para><b>What still is NOT here.</b> The DB (<c>_factory</c>/<c>_dbReady</c>), DB
/// seeding, settings reads, output-device choice, and <see cref="AnalysisStack.AttachDatabase"/>
/// stay in <c>App.InitializeServices</c>, deliberately deferred so the window paints
/// its first frame before Sqlite opens — hoisting them would change startup behaviour,
/// not just where the code lives. The controller/orchestrator/recognizer/bus/bindings
/// cluster, the view model, the audio engine, and the stats timer all need the live
/// window and stay in <c>App</c> too. See <c>~/Projects/sholto-refactor-plan.md</c>'s
/// "R2 retargeted" revision for the full tier breakdown.</para>
/// </summary>
public sealed record SholtoStack(
    ExternalToolStack ToolStack,
    FlacDecodeStrategy FlacStrategy,
    IAudioFileDecoder Decoder,
    AudioDevices AudioDevices,
    IPipeWireRouter PipeWireRouter,
    SholtoStorage Storage,
    ITrackScanner TrackScanner,
    AnalysisStack AnalysisStack,
    IDeckFactory DeckFactory)
{
    /// <summary>Builds every leaf above, in the same order App used to build them
    /// inline. Called once, from <c>Program.BuildAvaloniaApp</c>, before any Avalonia
    /// type exists.</summary>
    public static SholtoStack Build()
    {
        // Composition root for the arm's-length external tools (madmom, demucs,
        // ffmpeg) — see ExternalToolStack for what's inside and why.
        var toolStack = ExternalToolStack.FromEnvironment();

        // Composition root for audio decoding: the strategy list is built once
        // here rather than hardcoded inside AudioFileDecoder, so this is the only
        // place that needs to know the decoder lineup. FlacDecodeStrategy is kept
        // out separately so StartAudioAsync can hand it the SoundFlow engine once
        // AudioEngine creates it — see AttachEngine there.
        var flacStrategy = new FlacDecodeStrategy();
        var decoder = new AudioFileDecoder(
        [
            new Mp3DecodeStrategy(),
            flacStrategy,
            new WavDecodeStrategy(),
            new FfmpegDecodeStrategy(toolStack.FfmpegPath),
        ]);

        // Stems: the raw step (runs demucs unconditionally) and the cache
        // (pure filesystem query, same DemucsWorkspaceFor) are separate
        // components on the stack — see ExternalToolStack/DemucsStemAnalysisStep/
        // DemucsStemPresence. Deck gets the CACHING decorator (cache hit -> instant,
        // miss -> real run) since it's on the "double-click to load" path where a
        // hit should cost nothing; MainViewModel gets the bare cache for
        // HydrateStemStateAsync, which must NEVER trigger analysis for the
        // hundreds of rows it checks at startup.
        var demucs = toolStack.Stems;
        var madmom = toolStack.Beats;

        // IDeckFactory needs a real (never-null) reporter, AnalysisProvider
        // and key/grid cache ports up front, but the DB — and the DB-backed
        // tiers built from it — doesn't exist yet: the window paints its
        // first frame before InitializeServices opens the DB. AnalysisStack
        // builds all of that ONCE, here, over switchable DB-tier wrappers
        // that start as null objects; every Deck gets a real AnalysisProvider/
        // cache object immediately — it just degrades to memory-only analysis
        // and no persisted key/grid cache until InitializeServices calls
        // AttachDatabase once the DB opens, which upgrades already-built decks
        // in place. See AnalysisStack for the swap mechanism and the risk if
        // that call is ever skipped.
        var analysisStack = new AnalysisStack(madmom);

        var deckFactory = new DeckFactory(
            decoder, demucs, analysisStack.Reporter,
            analysisStack.Provider,
            analysisStack.KeyStore,
            analysisStack.GridStore,
            analysisStack.Peaks,
            analysisStack.KeyAnalyzer,
            analysisStack.VocalRegions,
            analysisStack.BeatgridFitter,
            // The ordered post-mix effect chain — EQ, filter, echo, in
            // that order — is decided here at the composition root, not
            // inside Deck/DeckFactory. See DeckEffectFactories's doc:
            // a 4th effect is one more entry in that list, no change here.
            DeckEffectFactories.Default);

        // Pass 1 chunk 7: the rest of the world-touching statics, composed once
        // here rather than reached through a static type name from wherever
        // they're needed.
        return new SholtoStack(
            toolStack,
            flacStrategy,
            decoder,
            new AudioDevices(),
            new PipeWireRouter(),
            new SholtoStorage(),
            new TrackScanner(),
            analysisStack,
            deckFactory);
    }
}
