using Sholto.App.Analysis.Analyzers.Waveform;

namespace Sholto.App.Analysis.Analyzers;

/// <summary>
/// First-pass track analysis: waveform peaks (with per-band amplitudes), BPM,
/// beat timestamps, and real downbeats. Everything needed to render the track
/// + beat-grid in one record.
/// </summary>
public sealed record BasicAnalysis(
    WaveformPeaks Peaks,
    double Bpm,
    double[] BeatTimes,
    double[] DownbeatTimes);
