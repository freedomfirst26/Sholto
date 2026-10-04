using Sholto.App.Analysis;
using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis.Analyzers.Keys;
using Sholto.App.Analysis.Analyzers.Vocals;
using Sholto.App.Analysis.Stages;
using Sholto.App.Analysis.Reporting;
using Sholto.App.Analysis.Stems;
using Sholto.App.Analysis.Stores;

namespace Sholto.App.Audio;

/// <summary>
/// BPM/key/stem analysis orchestration for the track currently on a deck,
/// extracted out of <see cref="TrackLoading"/> (see that class's header for
/// the full history of the Deck extraction programme).
///
/// <para><b>What stayed behind and why.</b> <see cref="TrackLoading.SwitchToStemMode"/>
/// does NOT move here even though it's triggered by this class's stem analysis
/// landing: it's the one point in this cluster that touches the live audio
/// graph (rebuilds the <c>SoundPlayer</c>, interacts with the held scratch
/// provider) and stays with the rest of that state on <c>TrackLoading</c>.
/// This class calls it back through the narrow <c>onStemsReady</c> delegate,
/// at the exact point in the sequence <c>TrackLoading.Load</c> used to call it
/// directly — after the stem paths and vocal regions are on <c>Analysis</c>, after
/// <c>AnalysisUpdated</c> — so stem-mix switchover is neither reordered nor duplicated.
/// The stem stage (<see cref="IAnalysisStage{TResult}"/> of <see cref="StemAnalysis"/>)
/// runs end to end: separation, decode, vocal regions. It hands the decoded
/// <see cref="StemSamples"/> (~370 MB) straight to <c>onStemsReady</c>; the
/// <see cref="StemAnalysis"/> itself is never stored on <c>Analysis</c>, which
/// would pin those buffers for the life of the track.</para>
///
/// <para><b>Write-backs.</b> <c>setDetectedBasic</c> and <c>setSampleCount</c>
/// are narrow delegates onto state <c>TrackLoading</c>/<c>DeckBeatgrid</c> own
/// (see their own docs) — same pattern as every other cross-component call in
/// this codebase, no back-reference to a concrete sibling type.</para>
/// </summary>
internal sealed class TrackAnalysisRun(
    IAnalysisProvider analysisProvider,
    IKeyAnalysisStore keyCache,
    IKeyAnalyzer keyAnalyzer,
    IAnalysisStage<StemAnalysis> stems,
    IAnalysisReporter reporter,
    IAudioFileDecoder decoder,
    Action<BasicAnalysis> setDetectedBasic,
    Action<long> setSampleCount,
    Action<StemSamples> onStemsReady) : ITrackAnalysisRun
{
    private readonly IAudioFileDecoder _decoder = decoder;
    private readonly IKeyAnalysisStore _keyCache = keyCache;
    private readonly IKeyAnalyzer _keyAnalyzer = keyAnalyzer;
    private readonly IAnalysisStage<StemAnalysis> _stems = stems;

    private readonly Action<BasicAnalysis> _setDetectedBasic = setDetectedBasic;
    private readonly Action<long> _setSampleCount = setSampleCount;
    private readonly Action<StemSamples> _onStemsReady = onStemsReady;

    /// <inheritdoc/>
    public IAnalysisProvider AnalysisProvider { get; } = analysisProvider;
    /// <inheritdoc/>
    public IAnalysisReporter Reporter { get; } = reporter;
    /// <inheritdoc/>
    public TrackAnalysis Analysis { get; set; } = new();

    /// <inheritdoc/>
    public event Action? AnalysisUpdated;

    /// <inheritdoc/>
    public void KickOffAnalysisFor(string filePath)
    {
        _ = Task.Run(async () =>
        {
            float[]? samples = null;
            try
            {
                // Decode once for both basic and key analysis. After both finish
                // we drop the reference so the ~92 MB float[] can be GC'd.
                samples = _decoder.Decode(filePath);
                int sampleRate = AudioFileDecoder.TargetSampleRate;

                // Update the visible sample count now that we have the exact value.
                _setSampleCount(samples.Length / 2);

                var track = new DecodedTrack(filePath, samples, sampleRate, AudioFileDecoder.TargetChannels);
                await RunBasicPhaseAsync(track);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Deck] background analysis failed: {ex.Message}");
            }
            finally
            {
                // Drop our reference to the decoded buffer. The GetAsync / KeyAnalyzer
                // calls have already consumed what they need; nothing else holds it.
                samples = null;
            }
        });
    }

    /// <inheritdoc/>
    public void KickOffAnalysis(DecodedTrack track)
    {
        _ = Task.Run(async () =>
        {
            await RunBasicPhaseAsync(track);

            // Stems have NO data dependency on Basic — KickOffStemAnalysis takes only
            // a file path and demucs opens the file itself. This sequencing is
            // deliberate CPU scheduling, not a correctness fix: demucs saturates every
            // core for 30-180s, and running it concurrently with madmom (basic) and the
            // key pass would compete for CPU exactly while the user is waiting for a
            // beatgrid. Do NOT "optimise" this back to parallel.
            await RunAdvancedPhaseAsync(track);
        });
    }

    private async Task<KeyAnalysis?> ComputeKeyAsync(DecodedTrack track)
    {
        try
        {
            try { var cached = await _keyCache.TryGetAsync(track.FilePath); if (cached is not null) return cached; }
            catch (Exception ex) { Console.WriteLine($"[Deck] key cache lookup failed: {ex.Message}"); }

            var key = await _keyAnalyzer.AnalyzeAsync(track, reporter: Reporter);

            try { await _keyCache.PutAsync(track.FilePath, key); }
            catch (Exception ex) { Console.WriteLine($"[Deck] key cache write failed: {ex.Message}"); }

            return key;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Deck] key analysis failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>Basic (BPM/beats) + key analysis for the in-memory <see cref="TrackLoading.Load"/>
    /// path, run concurrently — same shape as <see cref="KickOffAnalysisFor"/>. Deck plays
    /// immediately; the beat grid and key appear when this lands. Awaited by
    /// <see cref="KickOffAnalysis"/> before it starts the advanced (stems) phase.</summary>
    private async Task RunBasicPhaseAsync(DecodedTrack track)
    {
        var basicTask = AnalysisProvider.GetAsync(track);
        var keyTask   = ComputeKeyAsync(track);

        // Basic and key are independent results from here on: one failing must
        // not discard the other. Each gets its own try/catch so a fault on
        // either task is observed and handled without affecting the other's
        // outcome (see class header regression note in TrackAnalysisRun).
        bool updated = false;

        try
        {
            var basic = await basicTask;
            Console.WriteLine($"[Deck] analysis: {basic.Bpm:F1} BPM, {basic.BeatTimes.Length} beats, {basic.DownbeatTimes.Length} downbeats");
            _setDetectedBasic(basic);
            updated = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Deck] background analysis failed: {ex.Message}");
        }

        try
        {
            var key = await keyTask;
            if (key is not null)
            {
                Console.WriteLine($"[Deck] key: {key.Key?.ToCamelot()}");
                Analysis.Set(key);
                updated = true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Deck] background analysis failed: {ex.Message}");
        }

        if (updated)
        {
            AnalysisUpdated?.Invoke();
        }
    }

    /// <summary>Stem separation for the in-memory <see cref="TrackLoading.Load"/> path.
    /// Slower and isolated from playback; on completion, auto-switches the deck to
    /// stem-mix playback via the <c>onStemsReady</c> callback handed in at construction.
    /// Awaited by <see cref="KickOffAnalysis"/> after the basic phase completes.</summary>
    private async Task RunAdvancedPhaseAsync(DecodedTrack track)
    {
        // Stems run independently of the BPM pipeline — slower (demucs takes 30-180s
        // on CPU for one track) and isolated from playback. Cached on disk so we only
        // pay the cost the first time a track is loaded ever.
        if (!_stems.IsAvailable)
        {
            Console.WriteLine("[Deck] stems skipped: demucs not available");
            return;
        }

        var loadedPath = track.FilePath;
        try
        {
            var r = await _stems.RunAsync(track);
            Analysis.Set(r.Paths);
            // Vocal regions must be Set LAST — DeckSession subscribes to
            // VocalRegionsReady to re-emit the deck's presence layer.
            Analysis.Set<IReadOnlyList<VocalRegion>>(r.Vocals);
            Console.WriteLine($"[Deck] stems ready: {Path.GetDirectoryName(r.Paths.Vocals)}");
            AnalysisUpdated?.Invoke();

            // Auto-switch this deck to stem-mix playback so per-stem mute is live.
            // Skip if user already moved on to a different track in the meantime.
            if (loadedPath == track.FilePath)
                _onStemsReady(r.Samples);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Deck] stem analysis failed: {ex.Message}");
        }
    }
}
