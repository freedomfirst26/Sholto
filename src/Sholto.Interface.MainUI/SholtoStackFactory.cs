using Sholto.App.Analysis.Stages;
using Sholto.Interface.MainUI.Models;
using Sholto.App.Audio;
using Sholto.App.Dsp;
using Sholto.App.ExternalTools;
using Sholto.App.Library;
using Sholto.App.Settings;
using Sholto.App.Storage;
using Sholto.App.Lifecycle;
using SoundFlow.Enums;
using SoundFlow.Structs;

namespace Sholto.Interface.MainUI;

/// <summary>Builds the <see cref="SholtoStack"/>: every plain-.NET leaf, in the same
/// order App used to build them inline. Called once, from
/// <c>Program.BuildAvaloniaApp</c> (or the designer-only <c>App()</c> constructor),
/// before any Avalonia type exists.
///
/// <para>The sub-factories and the storage are handed in by the composition roots
/// (<c>Program.BuildAvaloniaApp</c> and the designer-only <c>App()</c>), which build
/// one <c>KeyFactory</c> and share it between the analysis stack factory and the
/// storage's database factory.</para></summary>
public sealed class SholtoStackFactory(
    IExternalToolStackFactory toolStackFactory,
    IAnalysisStackFactory analysisStackFactory,
    SholtoStorage storage)
{
    private readonly IExternalToolStackFactory _toolStackFactory = toolStackFactory;
    private readonly IAnalysisStackFactory _analysisStackFactory = analysisStackFactory;
    private readonly SholtoStorage _storage = storage;

    public SholtoStack Build()
    {
        // Composition root for the arm's-length external tools (madmom, demucs,
        // ffmpeg) — see ExternalToolStack for what's inside and why.
        var toolStack = _toolStackFactory.Build();

        // Names and logs the boot-time tool probe that ToolSet's constructor
        // already ran (see ExternalToolStack.Check) — previously discarded, which
        // is why a missing madmom/demucs/ffmpeg install was invisible at startup.
        Console.WriteLine($"[Tools] system check: {toolStack.Check.Summary}");
        if (toolStack.Check.AnyRequiredMissing)
        {
            Console.WriteLine(
                "[Tools] beat detection unavailable — no BPM, beatgrid, waveform or key for any track. " +
                $"Install: {MadmomBeatAnalysisStep.InstallCommand}");
        }

        // Composition root for audio decoding: the strategy list is built once
        // here rather than hardcoded inside AudioFileDecoder, so this is the only
        // place that needs to know the decoder lineup. FlacDecodeStrategy is kept
        // out separately so StartAudioAsync can hand it the SoundFlow engine once
        // AudioEngine creates it — see AttachEngine there.
        var naudioDecoding = new NAudioDecoding();
        // The one deck format (48 kHz stereo F32): FLAC decodes straight to it, and
        // AudioEngine attaches every deck with it — carried on the stack for both.
        var deckFormat = new AudioFormat
        {
            SampleRate = AudioFileDecoder.TargetSampleRate,
            Channels = 2,
            Format = SampleFormat.F32,
        };
        var flacStrategy = new FlacDecodeStrategy(deckFormat);
        var decoder = new AudioFileDecoder(
        [
            new Mp3DecodeStrategy(naudioDecoding),
            flacStrategy,
            new WavDecodeStrategy(naudioDecoding),
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

        // The library database object is built first so the analysis stack can defer its
        // persistent stores on LibraryDatabase.Opened. IDeckFactory needs real (never-null)
        // provider and key/grid ports up front, but the DB opens later (the window paints
        // before InitializeServices opens it); the deferred stores await it and then forward.
        var libraryDatabase = new LibraryDatabase(_storage);
        var analysisStack = _analysisStackFactory.Build(madmom, libraryDatabase);

        var stemAnalysisStage = new StemAnalysisStage(demucs, new StemDecoder(decoder), analysisStack.Peaks, analysisStack.VocalRegions, analysisStack.Reporter);

        var deckFactory = new DeckFactory(
            decoder, stemAnalysisStage, analysisStack.Reporter,
            analysisStack.Provider,
            analysisStack.KeyStore,
            analysisStack.GridStore,
            analysisStack.KeyAnalyzer,
            analysisStack.BeatgridFactory,
            // The ordered post-mix effect chain — EQ, filter, echo, in
            // that order — is decided here at the composition root, not
            // inside Deck/DeckFactory. See DeckEffectFactory's doc:
            // a 4th effect is one more entry in that list, no change here.
            new DeckEffectFactory(new TempoMath()).Chain,
            new PlaybackProviderFactory());

        // Pass 1 chunk 7: the rest of the world-touching statics, composed once
        // here rather than reached through a static type name from wherever
        // they're needed.
        return new SholtoStack(
            toolStack,
            flacStrategy,
            decoder,
            new MiniAudioOutputEnumerator(),
            new PipeWireRouter(),
            libraryDatabase,
            new TrackScanner(),
            analysisStack,
            deckFactory,
            deckFormat,
            new TagRecency(),
            new EqualPowerCrossfade(),
            new LibrarySearch(),
            new ProcessStats(),
            new SettingPreference(libraryDatabase, SettingsKeys.Theme),
            new SettingPreference(libraryDatabase, SettingsKeys.MusicDir),
            new SettingPreference(libraryDatabase, SettingsKeys.OutputDevice));
    }
}
