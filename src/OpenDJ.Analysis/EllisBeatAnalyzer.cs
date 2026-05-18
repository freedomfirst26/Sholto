namespace OpenDJ.Analysis;

/// <summary>
/// In-process Ellis 2007 beat tracker: onset envelope → autocorrelation → DP.
/// Always available. Fallback when no external analyzer is installed.
/// </summary>
public sealed class EllisBeatAnalyzer : IBeatAnalyzer
{
    public bool IsAvailable => true;

    public Task<BeatResult> AnalyzeAsync(string filePath, float[] stereoSamples, int sampleRate, CancellationToken ct)
    {
        var (_, kickEnv, hop) = WaveformPeaks.ComputeWithOnsets(stereoSamples, channels: 2, sampleRate: sampleRate);
        var (bpm, beats) = WaveformPeaks.EstimateTempo(kickEnv, hop, sampleRate);
        return Task.FromResult(new BeatResult(bpm, beats, DownbeatTimes: null));
    }
}
