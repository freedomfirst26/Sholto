using Sholto.App.Analysis.Analyzers.Vocals;
using Sholto.App.Analysis.Analyzers.Waveform;
using Sholto.App.Analysis.Processing;
using Sholto.App.Analysis.Reporting;
using Sholto.App.Analysis.Stems;

namespace Sholto.App.Analysis.Stages;

/// <summary>
/// Stem stage, end to end: separation through <see cref="IStemAnalysisStep"/>
/// (demucs), one decode of the four stems through <see cref="IStemDecoder"/>, then
/// vocal peaks and vocal regions. Failures are reported like
/// <see cref="BasicAnalysisStage"/>.
/// </summary>
public sealed class StemAnalysisStage(
    IStemAnalysisStep step,
    IStemDecoder stemDecoder,
    IWaveformPeakAnalyzer peakAnalyzer,
    IVocalRegionAnalyzer vocalRegionAnalyzer,
    IAnalysisReporter reporter,
    IStemDevice device,
    IStemGate gate) : IStemStage
{
    private readonly IStemAnalysisStep _step = step;
    private readonly IStemDecoder _stemDecoder = stemDecoder;
    private readonly IWaveformPeakAnalyzer _peakAnalyzer = peakAnalyzer;
    private readonly IVocalRegionAnalyzer _vocalRegionAnalyzer = vocalRegionAnalyzer;
    private readonly IAnalysisReporter _reporter = reporter;
    private readonly IStemDevice _device = device;
    private readonly IStemGate _gate = gate;

    public bool IsAvailable => _step.IsAvailable;

    public async Task<bool> CanOverlapBasicAnalysisAsync(CancellationToken ct = default) =>
        await _device.ResolveAsync(ct) == StemDevice.Cuda;

    public Task<StemAnalysis> RunAsync(DecodedTrack track, CancellationToken ct = default) =>
        RunAsync(track.FilePath, track.SampleRate, track.Channels, ct);

    public async Task<StemAnalysis> RunAsync(string filePath, int sampleRate, int channels, CancellationToken ct = default)
    {
        // One demucs at a time across both decks; decoding the result is not gated.
        StemPaths paths;
        using (await _gate.EnterAsync(ct))
            paths = await _step.AnalyzeAsync(filePath, _reporter, ct);
        try
        {
            var samples = await _stemDecoder.DecodeAsync(paths, ct);

            // normalizeBands stays false so the vocal envelope is comparable
            // across tracks rather than rescaled to [0,1] per stem.
            var vocalPeaks = _peakAnalyzer.Compute(samples.Vocals, channels, sampleRate, normalizeBands: false);
            var regions = _vocalRegionAnalyzer.Analyze(vocalPeaks, sampleRate);
            return new StemAnalysis(paths, samples, regions);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Superseded by a newer load: not a failure, and not still running either.
            _reporter.Cancelled(filePath, AnalysisSteps.Stems);
            throw;
        }
        catch (Exception ex)
        {
            _reporter.Failed(filePath, AnalysisSteps.Stems, ex.Message);
            throw;
        }
    }
}
