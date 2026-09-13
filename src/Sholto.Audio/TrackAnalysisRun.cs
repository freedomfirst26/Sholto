using Sholto.Analysis;
using Sholto.Analysis.Analyzers;
using Sholto.Analysis.Processing;

namespace Sholto.Audio;

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
/// directly — after <c>Analysis.Set(stems)</c>, after <c>AnalysisUpdated</c> —
/// so stem-mix switchover is neither reordered nor duplicated.</para>
///
/// <para><b>Write-backs.</b> <c>setDetectedBasic</c> and <c>setSampleCount</c>
/// are narrow delegates onto state <c>TrackLoading</c>/<c>DeckBeatgrid</c> own
/// (see their own docs) — same pattern as every other cross-component call in
/// this codebase, no back-reference to a concrete sibling type.</para>
/// </summary>
internal sealed class TrackAnalysisRun : ITrackAnalysisRun
{
    private readonly IAudioFileDecoder _decoder;
    private readonly IKeyAnalysisStore _keyCache;
    private readonly IKeyAnalyzer _keyAnalyzer;
    private readonly IWaveformPeakAnalyzer _peakAnalyzer;
    private readonly IVocalRegionAnalyzer _vocalRegionAnalyzer;

    private readonly Action<BasicAnalysis> _setDetectedBasic;
    private readonly Action<long> _setSampleCount;
    private readonly Action<StemPaths> _onStemsReady;

    public TrackAnalysisRun(
        IAnalysisProvider analysisProvider,
        IKeyAnalysisStore keyCache,
        IKeyAnalyzer keyAnalyzer,
        IStemAnalysisStep stemAnalyzer,
        IWaveformPeakAnalyzer peakAnalyzer,
        IVocalRegionAnalyzer vocalRegionAnalyzer,
        IAnalysisReporter reporter,
        IAudioFileDecoder decoder,
        Action<BasicAnalysis> setDetectedBasic,
        Action<long> setSampleCount,
        Action<StemPaths> onStemsReady)
    {
        AnalysisProvider = analysisProvider;
        _keyCache = keyCache;
        _keyAnalyzer = keyAnalyzer;
        StemAnalyzer = stemAnalyzer;
        _peakAnalyzer = peakAnalyzer;
        _vocalRegionAnalyzer = vocalRegionAnalyzer;
        Reporter = reporter;
        _decoder = decoder;
        _setDetectedBasic = setDetectedBasic;
        _setSampleCount = setSampleCount;
        _onStemsReady = onStemsReady;
    }

    /// <inheritdoc/>
    public IAnalysisProvider AnalysisProvider { get; }
    /// <inheritdoc/>
    public IAnalysisReporter Reporter { get; }
    /// <inheritdoc/>
    public IStemAnalysisStep StemAnalyzer { get; }
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
                var basicTask = AnalysisProvider.GetAsync(track);
                var keyTask   = ComputeKeyAsync(track);
                await Task.WhenAll(basicTask, keyTask);

                var basic = await basicTask;
                var key = await keyTask;

                Console.WriteLine($"[Deck] analysis: {basic.Bpm:F1} BPM, {basic.BeatTimes.Length} beats, {basic.DownbeatTimes.Length} downbeats");
                _setDetectedBasic(basic);
                if (key is not null)
                {
                    Console.WriteLine($"[Deck] key: {key.KeyName} ({key.Camelot})");
                    Analysis.Set(key);
                }
                AnalysisUpdated?.Invoke();
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

    /// <inheritdoc/>
    public void KickOffBasicAnalysis(DecodedTrack track)
    {
        // Analysis runs off-thread; deck plays immediately, beat grid appears when ready.
        _ = Task.Run(async () =>
        {
            try
            {
                var basic = await AnalysisProvider.GetAsync(track);
                Console.WriteLine($"[Deck] analysis: {basic.Bpm:F1} BPM, {basic.BeatTimes.Length} beats, {basic.DownbeatTimes.Length} downbeats");
                _setDetectedBasic(basic);
                AnalysisUpdated?.Invoke();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Deck] analysis failed: {ex.Message}");
            }
        });
    }

    /// <inheritdoc/>
    public void KickOffKeyAnalysis(DecodedTrack track)
    {
        // Key estimation is independent of beats and stems — reads the same decoded
        // buffer the basic analysis used. Goertzel + Krumhansl-Schmuckler in-process,
        // no subprocess. Cached to the SQLite analyses table; on cache hit we skip the
        // chroma compute and just publish.
        _ = Task.Run(async () =>
        {
            try
            {
                KeyAnalysis? key = null;
                try { key = await _keyCache.TryGetAsync(track.FilePath); }
                catch (Exception ex) { Console.WriteLine($"[Deck] key cache lookup failed: {ex.Message}"); }

                if (key is null)
                {
                    key = await _keyAnalyzer.AnalyzeAsync(track, reporter: Reporter);
                    try { await _keyCache.PutAsync(track.FilePath, key); }
                    catch (Exception ex) { Console.WriteLine($"[Deck] key cache write failed: {ex.Message}"); }
                }
                Console.WriteLine($"[Deck] key: {key.KeyName} ({key.Camelot})");
                Analysis.Set(key);
                AnalysisUpdated?.Invoke();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Deck] key analysis failed: {ex.Message}");
            }
        });
    }

    /// <inheritdoc/>
    public void KickOffStemAnalysis(string filePath)
    {
        // Stems run independently of the BPM pipeline — slower (demucs takes 30-180s
        // on CPU for one track) and isolated from playback. Cached on disk so we only
        // pay the cost the first time a track is loaded ever.
        var loadedPath = filePath;
        _ = Task.Run(async () =>
        {
            try
            {
                var stems = await StemAnalyzer.AnalyzeAsync(filePath, Reporter);
                Analysis.Set(stems);
                Console.WriteLine($"[Deck] stems ready: {Path.GetDirectoryName(stems.Vocals)}");
                AnalysisUpdated?.Invoke();

                // Auto-switch this deck to stem-mix playback so per-stem mute is live.
                // Skip if user already moved on to a different track in the meantime.
                if (loadedPath == filePath)
                    _onStemsReady(stems);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Deck] stem analysis failed: {ex.Message}");
            }
        });
    }

    /// <inheritdoc/>
    public void KickOffStemPeaksAnalysis(StemSamples stems)
    {
        _ = Task.Run(() =>
        {
            try
            {
                int sr = AudioFileDecoder.TargetSampleRate;
                // NOTE: StemPeaks are no longer consumed by the UI — the main
                // waveform is a frequency-band view with a stable silhouette, so
                // stem toggles don't repaint it. This block is dead weight kept
                // for now; removing the per-stem peak computation is a pending
                // follow-up. normalizeBands stays false so, if it is ever revived,
                // the stems remain comparable to each other (per-stem [0,1]
                // normalisation would make a quiet vocal look as loud as a kick).
                var pd = Task.Run(() => _peakAnalyzer.Compute(stems.Drums,  channels: 2, sampleRate: sr, normalizeBands: false));
                var pv = Task.Run(() => _peakAnalyzer.Compute(stems.Vocals, channels: 2, sampleRate: sr, normalizeBands: false));
                var pb = Task.Run(() => _peakAnalyzer.Compute(stems.Bass,   channels: 2, sampleRate: sr, normalizeBands: false));
                var po = Task.Run(() => _peakAnalyzer.Compute(stems.Other,  channels: 2, sampleRate: sr, normalizeBands: false));
                Task.WaitAll(pd, pv, pb, po);
                Analysis.Set(new StemPeaks(pd.Result, pv.Result, pb.Result, po.Result));
                Console.WriteLine("[Deck] per-stem peaks computed");

                // Feed the vocal stem layer through the region analyzer and publish
                // the regions to the deck view (green "vocals present" rectangles).
                var regions = _vocalRegionAnalyzer.Analyze(pv.Result, sr);
                Analysis.Set(regions);
                Console.WriteLine($"[Deck] vocal regions: {regions.Regions.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Deck] per-stem peaks failed: {ex.Message}");
            }
        });
    }
}
