using System.Globalization;

using Sholto.App.Analysis.Analyzers.Beats;

namespace Sholto.App.ExternalTools;

/// <summary>
/// The one parser for beat-tracker output. Beat This! (its <c>.beats</c> file) and madmom's
/// DBNDownBeatTracker (its stdout) both write one line per beat, <c>TIME\tBEAT_NUMBER</c>,
/// where BEAT_NUMBER is 1 for a downbeat and 2..N for the rest of the bar. Moved out of
/// <c>MadmomTool</c> so both descriptors read beats the same way.
/// </summary>
public sealed class DetectedBeatsFactory : IDetectedBeatsFactory
{
    public DetectedBeats FromBeatLines(string text)
    {
        var beats = new List<double>();
        var downbeats = new List<double>();
        foreach (var line in text.Split('\n'))
        {
            var parts = line.Trim().Split('\t');
            if (parts[0].Length == 0) continue;
            if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var t)) continue;
            beats.Add(t);
            if (parts.Length >= 2 && parts[1].Trim() == "1") downbeats.Add(t);
        }
        return new DetectedBeats(BpmFromBeats(beats), beats.ToArray(), downbeats.ToArray());
    }

    /// <summary>Tempo (BPM, 0.1 resolution) from beat times. Uses the MEAN of the
    /// inter-beat gaps, not the median: a tracker quantizes beat times to its frame grid
    /// (madmom ~10 ms, Beat This! 20 ms), so a true gap that falls between two frames
    /// (174 BPM = 0.3448 s) is emitted as an alternating mix of the neighbouring
    /// quantized values (0.34 / 0.35). The median then locks onto whichever is slightly
    /// more common and biases the tempo high — a 174 BPM DnB track reads 176.5. The mean
    /// averages the quantization back to the true value. Gaps far from the median
    /// (missed or doubled beats) are trimmed first so dropouts don't drag it.</summary>
    public double BpmFromBeats(IReadOnlyList<double> beats)
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
