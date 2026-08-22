using Xunit;
using Sholto.Analysis;

namespace Sholto.App.Tests;

public class BpmFromBeatsTests
{
    private static double[] BeatsFromGaps(IEnumerable<double> gaps)
    {
        var times = new List<double> { 0.0 };
        double t = 0;
        foreach (var g in gaps) { t += g; times.Add(t); }
        return times.ToArray();
    }

    private static IEnumerable<double> Repeat(double[] pattern, int times)
    {
        for (int i = 0; i < times; i++)
            foreach (var p in pattern) yield return p;
    }

    [Fact]
    public void QuantizedGaps_UseMean_SoA174TrackDoesNotReadAs176()
    {
        // 174.7 BPM quantized to madmom's ~10ms grid → alternating 0.34/0.35 with
        // 0.34 slightly more common (as in the real DnB track). Median → 176.5.
        var beats = BeatsFromGaps(Repeat([0.34, 0.34, 0.35], 40));
        double bpm = MadmomBeatAnalyzer.BpmFromBeats(beats);
        Assert.InRange(bpm, 174.0, 175.5);            // mean → ~174.8
        Assert.True(bpm < 176.0, $"median would give ~176.5; got {bpm}");
    }

    [Fact]
    public void CleanTempo_IsExact()
    {
        var beats = BeatsFromGaps(Repeat([0.5], 50)); // 120 BPM
        Assert.Equal(120.0, MadmomBeatAnalyzer.BpmFromBeats(beats));
    }

    [Fact]
    public void MissedBeat_IsTrimmed_NotDraggingTempo()
    {
        var gaps = Repeat([0.34, 0.34, 0.35], 40).ToList();
        gaps[20] = 0.68; // one dropped beat → a doubled gap
        double bpm = MadmomBeatAnalyzer.BpmFromBeats(BeatsFromGaps(gaps));
        Assert.InRange(bpm, 174.0, 175.5);
    }

    [Fact]
    public void TooFewBeats_ReturnsZero()
    {
        Assert.Equal(0.0, MadmomBeatAnalyzer.BpmFromBeats([0.0, 0.5]));
    }
}
