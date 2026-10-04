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
    IAnalysisReporter reporter) : IAnalysisStage<StemAnalysis>
{
    private readonly IStemAnalysisStep _step = step;
    private readonly IStemDecoder _stemDecoder = stemDecoder;
    private readonly IWaveformPeakAnalyzer _peakAnalyzer = peakAnalyzer;
    private readonly IVocalRegionAnalyzer _vocalRegionAnalyzer = vocalRegionAnalyzer;
    private readonly IAnalysisReporter _reporter = reporter;

    public bool IsAvailable => _step.IsAvailable;

    public async Task<StemAnalysis> RunAsync(DecodedTrack track, CancellationToken ct = default)
    {
        var paths = await _step.AnalyzeAsync(track.FilePath, _reporter, ct);
        try
        {
            var samples = await _stemDecoder.DecodeAsync(paths, ct);

            // normalizeBands stays false so the vocal envelope is comparable
            // across tracks rather than rescaled to [0,1] per stem.
            var vocalPeaks = _peakAnalyzer.Compute(samples.Vocals, track.Channels, track.SampleRate, normalizeBands: false);
            var regions = _vocalRegionAnalyzer.Analyze(vocalPeaks, track.SampleRate);
            return new StemAnalysis(paths, samples, regions);
        }
        catch (Exception ex)
        {
            _reporter.Failed(track.FilePath, AnalysisSteps.Stems, ex.Message);
            throw;
        }
    }
}
