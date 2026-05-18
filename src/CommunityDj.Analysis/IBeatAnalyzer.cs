namespace CommunityDj.Analysis;

/// <summary>
/// Pluggable beat / tempo detector. Implementations vary in accuracy and cost:
///   - <c>EllisBeatAnalyzer</c>: pure C#, no deps, librosa-parity quality (good).
///   - <c>MadmomBeatAnalyzer</c>: shells out to madmom's DBNDownBeatTracker, RNN +
///     dynamic Bayesian network, identifies real downbeats (best, slower, needs Python+ffmpeg).
/// </summary>
public interface IBeatAnalyzer
{
    /// <summary>True if this analyzer is ready to run on the current system.</summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Analyze a decoded stereo float buffer (44.1 kHz). Caller may also pass
    /// the original file path — some analyzers prefer to read the file directly.
    /// </summary>
    Task<BeatResult> AnalyzeAsync(string filePath, float[] stereoSamples, int sampleRate, CancellationToken ct);
}

public sealed record BeatResult(
    double Bpm,
    double[] BeatTimes,
    double[]? DownbeatTimes
);
