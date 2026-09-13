using System.Globalization;

using Sholto.Analysis;
using Sholto.Analysis.Processing;

namespace Sholto.ExternalTools;

/// <summary>Descriptor for madmom's DBNDownBeatTracker. Required — no fallback if
/// this fails or isn't installed. Postcondition: stdout parses to at least one beat.</summary>
internal sealed class MadmomTool : IToolDefinition<DetectedBeats>
{
    public string Name => AnalysisSteps.Beats;
    public string BinaryName => ExternalToolNames.Madmom;
    public bool IsRequired => true;

    public IReadOnlyList<string> BuildArgs(ToolInput toolInput) =>
        new[] { "--beats_per_bar", "3,4", "single", toolInput.Input };

    public ToolOutcome<DetectedBeats> Verify(
        ToolInput toolInput, string stdout, int exitCode)
    {
        if (exitCode != 0)
            return ToolOutcome<DetectedBeats>.Failure(
                $"madmom exited with code {exitCode}");

        // DBNDownBeatTracker output: each line is "TIME\tBEAT_NUMBER" where
        // BEAT_NUMBER is 1 for downbeat, 2..N for the rest of the bar.
        var beats = new List<double>();
        var downbeats = new List<double>();
        foreach (var line in stdout.Split('\n'))
        {
            var parts = line.Trim().Split('\t');
            if (parts.Length < 1 || parts[0].Length == 0) continue;
            if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var t)) continue;
            beats.Add(t);
            if (parts.Length >= 2 && parts[1].Trim() == "1") downbeats.Add(t);
        }

        if (beats.Count == 0)
            return ToolOutcome<DetectedBeats>.Failure(
                "madmom exited 0 but produced no beats");

        double bpm = BpmFromBeats(beats);
        return ToolOutcome<DetectedBeats>.Success(new DetectedBeats(bpm, beats.ToArray(), downbeats.ToArray()));
    }

    /// <summary>Tempo (BPM, 0.1 resolution) from beat times. Uses the MEAN of the
    /// inter-beat gaps, not the median: madmom quantizes beat times to its ~10 ms
    /// frame grid, so a true gap that falls between two frames (174 BPM = 0.3448 s)
    /// is emitted as an alternating mix of the neighbouring quantized values
    /// (0.34 / 0.35). The median then locks onto whichever is slightly more common
    /// and biases the tempo high — a 174 BPM DnB track reads 176.5. The mean
    /// averages the quantization back to the true value. Gaps far from the median
    /// (missed or doubled beats) are trimmed first so dropouts don't drag it.
    ///
    /// Pure tempo maths, no dependency on anything else in this file or in
    /// <c>Sholto.Analysis</c> — kept beside the only caller (<see cref="Verify"/>)
    /// rather than on the analysis-step orchestrator that used to own it.</summary>
    public static double BpmFromBeats(IReadOnlyList<double> beats)
    {
        if (beats.Count < 3) return 0;

        var gaps = new double[beats.Count - 1];
        for (int i = 1; i < beats.Count; i++) gaps[i - 1] = beats[i] - beats[i - 1];
        Array.Sort(gaps);
        double median = gaps[gaps.Length / 2];
        if (median <= 0) return 0;

        double lo = median * 0.5, hi = median * 1.5;
        double sum = 0;
        int n = 0;
        foreach (var g in gaps)
            if (g >= lo && g <= hi) { sum += g; n++; }

        double mean = n > 0 ? sum / n : median;
        return Math.Round(60.0 / mean * 10) / 10.0;
    }
}
