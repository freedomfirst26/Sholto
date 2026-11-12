using Microsoft.Extensions.Options;
using Sholto.App.Analysis;
using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis.Analyzers.Keys;
using Sholto.App.Analysis.Reporting;
using Sholto.App.Analysis.Stores;
using Sholto.Data;

namespace Sholto.App.Audio.BatchAnalysis;

/// <summary>
/// <see cref="IBatchAnalyzer"/> over the same basic-analysis provider chain and key store a deck
/// uses, so whatever a batch stores a later deck load reads as a cache hit.
///
/// <para><b>Skip check before decode.</b> A track whose basic analysis is in
/// <paramref name="basicStore"/> and whose key is in <paramref name="keyStore"/> is skipped without
/// decoding. The provider cannot answer that on its own: its <c>Begin(path)</c> does hit the caches
/// path-only, but the result is only observable through <c>CompleteAsync(track)</c>, which needs the
/// decoded samples on a miss. So the path-only store is asked first.</para>
///
/// <para><b>Memory.</b> The decoded buffer lives only for the length of one track's work, as in
/// <c>TrackAnalysisRun</c>, so memory stays near MaxParallelTracks × one decoded track.</para>
///
/// <para><b>Progress.</b> Reported through <see cref="IProgress{T}"/>; the analysis reporter handed
/// to the key analyser is the do-nothing one, so library rows are not touched by a batch.</para>
/// </summary>
public sealed class BatchAnalyzer(
    IAudioFileDecoder decoder,
    IAnalysisProvider analysisProvider,
    IBasicAnalysisStore basicStore,
    IKeyAnalysisStore keyStore,
    IKeyAnalyzer keyAnalyzer,
    IOptions<BatchAnalysisOptions> options,
    ILog log) : IBatchAnalyzer
{
    private readonly IAudioFileDecoder _decoder = decoder;
    private readonly IAnalysisProvider _analysisProvider = analysisProvider;
    private readonly IBasicAnalysisStore _basicStore = basicStore;
    private readonly IKeyAnalysisStore _keyStore = keyStore;
    private readonly IKeyAnalyzer _keyAnalyzer = keyAnalyzer;
    private readonly ILog _log = log;
    private readonly int _maxParallelTracks = Math.Max(1, options.Value.MaxParallelTracks);
    private readonly IAnalysisReporter _reporter = new NullAnalysisReporter();

    public Task RunAsync(IReadOnlyList<string> paths, IProgress<BatchTrackProgress> progress, CancellationToken ct)
    {
        var parallel = new ParallelOptions { MaxDegreeOfParallelism = _maxParallelTracks, CancellationToken = ct };
        // ForEachAsync takes no new item once ct is cancelled; each body catches its own failure,
        // so one track throwing never cancels the loop for the rest.
        return Parallel.ForEachAsync(paths, parallel, (path, token) => RunTrackAsync(path, progress, token));
    }

    private async ValueTask RunTrackAsync(string path, IProgress<BatchTrackProgress> progress, CancellationToken ct)
    {
        progress.Report(new BatchTrackProgress(path, BatchTrackState.Started));
        try
        {
            var state = await AnalyseAsync(path, ct).ConfigureAwait(false);
            progress.Report(new BatchTrackProgress(path, state));
        }
        catch (Exception ex)
        {
            progress.Report(new BatchTrackProgress(path, BatchTrackState.Failed, ex.Message));
        }
    }

    private async Task<BatchTrackState> AnalyseAsync(string path, CancellationToken ct)
    {
        var storedBasic = await TryGetBasicAsync(path).ConfigureAwait(false);
        var storedKey = await TryGetKeyAsync(path).ConfigureAwait(false);
        if (storedBasic is not null && storedKey is not null) return BatchTrackState.Skipped;
        ct.ThrowIfCancellationRequested();

        // Begin before decoding so a beat-tracker miss overlaps the decode, as a deck prestart does.
        var request = await Task.Run(() => _analysisProvider.Begin(path, ct), ct).ConfigureAwait(false);
        var samples = await Task.Run(() => _decoder.Decode(path), ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();

        var track = new DecodedTrack(path, samples, AudioFileDecoder.TargetSampleRate, AudioFileDecoder.TargetChannels);
        await Task.WhenAll(
            request.CompleteAsync(track),
            storedKey is null ? ComputeKeyAsync(track, ct) : Task.CompletedTask).ConfigureAwait(false);
        return BatchTrackState.Finished;
    }

    private async Task<BasicAnalysis?> TryGetBasicAsync(string path)
    {
        try { return await _basicStore.TryGetAsync(path).ConfigureAwait(false); }
        catch (Exception ex)
        {
            _log.Error($"basic analysis lookup failed for {path}: {ex.Message}");
            return null;
        }
    }

    private async Task<KeyAnalysis?> TryGetKeyAsync(string path)
    {
        try { return await _keyStore.TryGetAsync(path).ConfigureAwait(false); }
        catch (Exception ex)
        {
            _log.Error($"key cache lookup failed for {path}: {ex.Message}");
            return null;
        }
    }

    /// <summary>Compute then store the key, the shape of <c>TrackAnalysisRun.ComputeKeyAsync</c>
    /// after its cache lookup. A failed write is logged, not failed: the key is still right, the
    /// next run just computes it again.</summary>
    private async Task ComputeKeyAsync(DecodedTrack track, CancellationToken ct)
    {
        var key = await _keyAnalyzer.AnalyzeAsync(track, _reporter, ct).ConfigureAwait(false);
        try { await _keyStore.PutAsync(track.FilePath, key).ConfigureAwait(false); }
        catch (Exception ex) { _log.Error($"key cache write failed for {track.FilePath}: {ex.Message}"); }
    }
}
