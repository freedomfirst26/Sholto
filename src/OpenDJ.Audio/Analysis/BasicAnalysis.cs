namespace OpenDJ.Audio.Analysis;

/// <summary>
/// First-pass track analysis: waveform peaks (with per-band amplitudes), BPM,
/// and beat timestamps. This is what's needed to render the track and beat-grid.
/// </summary>
public sealed record BasicAnalysis(
    WaveformPeaks Peaks,
    double Bpm,
    double[] BeatTimes) : IAnalysis
{
    public string Name => "Basic";

    public static BasicAnalysis Empty { get; } = new(WaveformPeaks.Empty, 0.0, []);

    public static BasicAnalysis Compute(float[] stereoSamples, int channels, int sampleRate)
    {
        var (peaks, kickEnv, hop) = WaveformPeaks.ComputeWithOnsets(stereoSamples, channels, sampleRate);
        var (bpm, beats) = WaveformPeaks.EstimateTempo(kickEnv, hop, sampleRate);
        return new BasicAnalysis(peaks, bpm, beats);
    }
}
