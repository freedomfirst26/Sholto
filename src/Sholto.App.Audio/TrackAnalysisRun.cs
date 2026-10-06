using Sholto.App.Analysis;
using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis.Analyzers.Keys;
using Sholto.App.Analysis.Analyzers.Vocals;
using Sholto.App.Analysis.Stages;
using Sholto.App.Analysis.Reporting;
using Sholto.App.Analysis.Stems;
using Sholto.App.Analysis.Stores;
using Sholto.Data;

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
/// <para><b>Threading and supersession.</b> The compute runs on the thread pool;
/// every result is applied on the app thread through <see cref="IAppThread"/>.
/// Each run is tied to the load generation that started it: assigning
/// <see cref="Analysis"/> (which <see cref="TrackLoading"/> does on every load,
/// begin-load and unload) bumps the generation and cancels the run in flight, so
/// its madmom/demucs process is killed at the ExternalTools boundary, and a result
/// that still arrives is dropped on the app thread because its generation is no
/// longer current.</para>
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
    IStemStage stems,
    IAnalysisReporter reporter,
    IAudioFileDecoder decoder,
    IAppThread appThread,
    Action<BasicAnalysis> setDetectedBasic,
    Action<long> setSampleCount,
    Action<StemSamples> onStemsReady) : ITrackAnalysisRun
{
    private readonly IAudioFileDecoder _decoder = decoder;
    private readonly IKeyAnalysisStore _keyCache = keyCache;
    private readonly IKeyAnalyzer _keyAnalyzer = keyAnalyzer;
    private readonly IStemStage _stems = stems;
    private readonly IAppThread _appThread = appThread;

    // App thread only. _generation names the load the current run belongs to;
    // _cts is that run's cancellation, cancelled when the next load supersedes it.
    private int _generation;
    private CancellationTokenSource _cts = new();
    private TrackAnalysis _analysis = new();
    private PrestartedAnalysis? _prestart;

    private readonly Action<BasicAnalysis> _setDetectedBasic = setDetectedBasic;
    private readonly Action<long> _setSampleCount = setSampleCount;
    private readonly Action<StemSamples> _onStemsReady = onStemsReady;

    /// <inheritdoc/>
    public IAnalysisProvider AnalysisProvider { get; } = analysisProvider;
    /// <inheritdoc/>
    public IAnalysisReporter Reporter { get; } = reporter;
    /// <inheritdoc/>
    public TrackAnalysis Analysis
    {
        get => _analysis;
        set
        {
            _analysis = value;
            Supersede();
        }
    }

    /// <inheritdoc/>
    public event Action? AnalysisUpdated;

    public void Prestart(string filePath)
    {
        var ct = _cts.Token;
        _prestart = new PrestartedAnalysis(
            _generation,
            filePath,
            BeginBasic(filePath, ct),
            Task.Run(() => StartStemsIfOverlapAsync(filePath, ct)));
    }

    /// <inheritdoc/>
    public bool HasPrestartFor(string filePath) =>
        _prestart is { } p && p.Generation == _generation && p.FilePath == filePath;

    private Task<IBasicAnalysisRequest> BeginBasic(string filePath, CancellationToken ct) =>
        Task.Run(() => AnalysisProvider.Begin(filePath, ct));

    /// <summary>Starts the stem run now if stems may overlap basic analysis; null means they follow basic.</summary>
    private async Task<Task<StemAnalysis>?> StartStemsIfOverlapAsync(string filePath, CancellationToken ct)
    {
        if (!await CanOverlapAsync(ct)) return null;
        return _stems.RunAsync(filePath, AudioFileDecoder.TargetSampleRate, AudioFileDecoder.TargetChannels, ct);
    }

    /// <inheritdoc/>
    public void KickOffAnalysisFor(string filePath)
    {
        var generation = _generation;
        var ct = _cts.Token;
        _ = Task.Run(async () =>
        {
            float[]? samples = null;
            try
            {
                // Decode once for both basic and key analysis. After both finish
                // we drop the reference so the ~92 MB float[] can be GC'd.
                samples = _decoder.Decode(filePath);
                ct.ThrowIfCancellationRequested();
                int sampleRate = AudioFileDecoder.TargetSampleRate;

                // Update the visible sample count now that we have the exact value.
                long frames = samples.Length / 2;
                Apply(generation, () => _setSampleCount(frames));

                var track = new DecodedTrack(filePath, samples, sampleRate, AudioFileDecoder.TargetChannels);
                await RunBasicPhaseAsync(track, BeginBasic(filePath, ct), generation, ct);
            }
            catch (Exception ex)
            {
                // Cancelled runs (a newer load superseded this one) end however they end; stay silent.
                if (!ct.IsCancellationRequested)
                {
                    Console.WriteLine($"[Deck] background analysis failed: {ex.Message}");
                }
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
        var generation = _generation;
        var ct = _cts.Token;
        var adopted = HasPrestartFor(track.FilePath) ? _prestart : null;
        _prestart = null;
        _ = Task.Run(async () =>
        {
            var basicTask = RunBasicPhaseAsync(track, adopted?.Basic ?? BeginBasic(track.FilePath, ct), generation, ct);

            // A prestart that found stems may overlap has already started them.
            var early = adopted is null ? null : await adopted.Stems;
            if (early is not null)
            {
                await Task.WhenAll(basicTask, ApplyStemsAsync(early, generation, ct));
                return;
            }

            // Stems have NO data dependency on Basic: demucs opens the file itself. Whether
            // they may overlap it is a CPU-scheduling question. demucs on the CPU saturates
            // every core for 30-180 s and would starve madmom while the user waits for a
            // beatgrid, so it stays serial. On CUDA (confirmed once by the stage, 8 s for a
            // 60 s clip, ~10 s of CPU) there is nothing to protect, so it starts now.
            if (await CanOverlapAsync(ct))
            {
                await Task.WhenAll(basicTask, RunAdvancedPhaseAsync(track, generation, ct));
                return;
            }

            await basicTask;
            if (ct.IsCancellationRequested) return;
            await RunAdvancedPhaseAsync(track, generation, ct);
        });
    }

    /// <summary>Whether stems may start alongside basic analysis. Any failure to find out means no.</summary>
    private async Task<bool> CanOverlapAsync(CancellationToken ct)
    {
        try { return _stems.IsAvailable && await _stems.CanOverlapBasicAnalysisAsync(ct); }
        catch (Exception ex)
        {
            if (!ct.IsCancellationRequested) Console.WriteLine($"[Deck] stem device probe failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>A new load (or an unload) makes every run in flight stale: bump the
    /// generation so applied results are dropped, and cancel so the processes die.</summary>
    private void Supersede()
    {
        _prestart = null;
        _generation++;
        var superseded = _cts;
        _cts = new CancellationTokenSource();
        superseded.Cancel();
    }

    /// <summary>Apply a result on the app thread, unless a newer load has superseded
    /// the run that produced it. A throwing apply is logged, not left to the app thread.</summary>
    private void Apply(int generation, Action apply) => _appThread.Post(() =>
    {
        if (generation != _generation) return;
        try { apply(); }
        catch (Exception ex) { Console.WriteLine($"[Deck] applying analysis result failed: {ex.Message}"); }
    });

    private async Task<KeyAnalysis?> ComputeKeyAsync(DecodedTrack track, CancellationToken ct)
    {
        try
        {
            try { var cached = await _keyCache.TryGetAsync(track.FilePath); if (cached is not null) return cached; }
            catch (Exception ex) { Console.WriteLine($"[Deck] key cache lookup failed: {ex.Message}"); }

            var key = await _keyAnalyzer.AnalyzeAsync(track, reporter: Reporter, ct: ct);

            try { await _keyCache.PutAsync(track.FilePath, key); }
            catch (Exception ex) { Console.WriteLine($"[Deck] key cache write failed: {ex.Message}"); }

            return key;
        }
        catch (Exception ex)
        {
            // Cancelled runs (a newer load superseded this one) end however they end; stay silent.
            if (!ct.IsCancellationRequested)
            {
                Console.WriteLine($"[Deck] key analysis failed: {ex.Message}");
            }
            return null;
        }
    }

    /// <summary>Basic (BPM/beats) + key analysis for the in-memory <see cref="TrackLoading.Load"/>
    /// path, run concurrently — same shape as <see cref="KickOffAnalysisFor"/>. Deck plays
    /// immediately; the beat grid and key each appear the moment they are ready, neither waiting
    /// for the other. Awaited by <see cref="KickOffAnalysis"/> before it starts the advanced
    /// (stems) phase when stems may not overlap.</summary>
    private async Task RunBasicPhaseAsync(
        DecodedTrack track, Task<IBasicAnalysisRequest> request, int generation, CancellationToken ct)
    {
        var basicTask = CompleteBasicAsync(request, track);
        var keyTask   = ComputeKeyAsync(track, ct);

        // Basic and key are independent results: one failing must not discard the other, and
        // each is applied as soon as it lands (the key used to wait for the beat grid, which is
        // the slower of the two).
        var applied = await Task.WhenAll(
            ApplyBasicAsync(basicTask, generation, ct),
            ApplyKeyAsync(keyTask, generation, ct));

        if (applied.Any(updated => updated))
        {
            Apply(generation, () => AnalysisUpdated?.Invoke());
        }
    }

    private async Task<BasicAnalysis> CompleteBasicAsync(Task<IBasicAnalysisRequest> request, DecodedTrack track) =>
        await (await request).CompleteAsync(track);

    private async Task<bool> ApplyBasicAsync(Task<BasicAnalysis> basicTask, int generation, CancellationToken ct)
    {
        try
        {
            var basic = await basicTask;
            Console.WriteLine($"[Deck] analysis: {basic.Bpm:F1} BPM, {basic.BeatTimes.Length} beats, {basic.DownbeatTimes.Length} downbeats");
            Apply(generation, () => _setDetectedBasic(basic));
            return true;
        }
        catch (Exception ex)
        {
            // Cancelled runs (a newer load superseded this one) end however they end; stay silent.
            if (!ct.IsCancellationRequested)
            {
                Console.WriteLine($"[Deck] background analysis failed: {ex.Message}");
            }
            return false;
        }
    }

    private async Task<bool> ApplyKeyAsync(Task<KeyAnalysis?> keyTask, int generation, CancellationToken ct)
    {
        try
        {
            var key = await keyTask;
            if (key is null) return false;
            Console.WriteLine($"[Deck] key: {key.Key?.ToCamelot()}");
            Apply(generation, () => Analysis.Set(key));
            return true;
        }
        catch (Exception ex)
        {
            // Cancelled runs (a newer load superseded this one) end however they end; stay silent.
            if (!ct.IsCancellationRequested)
            {
                Console.WriteLine($"[Deck] background analysis failed: {ex.Message}");
            }
            return false;
        }
    }

    /// <summary>Stem separation for the in-memory <see cref="TrackLoading.Load"/> path.
    /// Slower and isolated from playback; on completion, auto-switches the deck to
    /// stem-mix playback via the <c>onStemsReady</c> callback handed in at construction.
    /// Awaited by <see cref="KickOffAnalysis"/> after the basic phase completes.</summary>
    private async Task RunAdvancedPhaseAsync(DecodedTrack track, int generation, CancellationToken ct)
    {
        // Stems run independently of the BPM pipeline — slower (demucs takes 30-180s
        // on CPU for one track) and isolated from playback. Cached on disk so we only
        // pay the cost the first time a track is loaded ever.
        if (!_stems.IsAvailable)
        {
            Console.WriteLine("[Deck] stems skipped: demucs not available");
            return;
        }

        await ApplyStemsAsync(_stems.RunAsync(track, ct), generation, ct);
    }

    /// <summary>Awaits a started stem run and applies it on the app thread if its load is still current.</summary>
    private async Task ApplyStemsAsync(Task<StemAnalysis> run, int generation, CancellationToken ct)
    {
        try
        {
            var r = await run;
            // Everything below touches Analysis and the live audio graph, so it runs on
            // the app thread, and only if this track is still the one on the deck.
            Apply(generation, () =>
            {
                Analysis.Set(r.Paths);
                // Vocal regions must be Set LAST — DeckSession subscribes to
                // VocalRegionsReady to re-emit the deck's presence layer.
                Analysis.Set<IReadOnlyList<VocalRegion>>(r.Vocals);
                Console.WriteLine($"[Deck] stems ready: {Path.GetDirectoryName(r.Paths.Vocals)}");
                AnalysisUpdated?.Invoke();

                // Auto-switch this deck to stem-mix playback so per-stem mute is live.
                _onStemsReady(r.Samples);
            });
        }
        catch (Exception ex)
        {
            // Cancelled runs (a newer load superseded this one) end however they end; stay silent.
            if (!ct.IsCancellationRequested)
            {
                Console.WriteLine($"[Deck] stem analysis failed: {ex.Message}");
            }
        }
    }
}
