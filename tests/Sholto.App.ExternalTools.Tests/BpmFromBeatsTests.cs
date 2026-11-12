using System.Globalization;
using Xunit;
using Sholto.App.Analysis.ToolBoundary;
using Sholto.App.Analysis.Reporting;

namespace Sholto.App.ExternalTools.Tests;

public class BpmFromBeatsTests
{
    // BpmFromBeats lives on DetectedBeatsFactory, the one parser both beat trackers
    // (Beat This! and madmom) use. It is reached the way production reaches it: the
    // beat lines ("TIME\tBEAT_NUMBER") are parsed and the tempo comes back as Bpm.
    private Task<double> BpmFromBeats(double[] beats)
    {
        var lines = string.Join('\n', beats.Select(b => b.ToString("R", CultureInfo.InvariantCulture) + "\t1"));
        return Task.FromResult(new DetectedBeatsFactory().FromBeatLines(lines).Bpm);
    }

    [Fact]
    public async Task Madmom_step_reports_the_same_tempo_through_its_stdout()
    {
        var beats = BeatsFromGaps(Repeat([0.5], 50)); // 120 BPM
        var stdout = string.Join('\n', beats.Select(b => b.ToString("R", CultureInfo.InvariantCulture) + "\t1"));
        var step = new MadmomBeatAnalysisStepFactory(new ToolOutcomeFactory(), new DetectedBeatsFactory())
            .Create(new StdoutReplayTool(stdout), new NullAnalysisReporter());
        Assert.Equal(120.0, (await step.AnalyzeAsync("/music/track.mp3")).Bpm);
    }

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
    public async Task QuantizedGaps_UseMean_SoA174TrackDoesNotReadAs176()
    {
        // 174.7 BPM quantized to madmom's ~10ms grid → alternating 0.34/0.35 with
        // 0.34 slightly more common (as in the real DnB track). Median → 176.5.
        var beats = BeatsFromGaps(Repeat([0.34, 0.34, 0.35], 40));
        double bpm = await BpmFromBeats(beats);
        Assert.InRange(bpm, 174.0, 175.5);            // mean → ~174.8
        Assert.True(bpm < 176.0, $"median would give ~176.5; got {bpm}");
    }

    [Fact]
    public async Task CleanTempo_IsExact()
    {
        var beats = BeatsFromGaps(Repeat([0.5], 50)); // 120 BPM
        Assert.Equal(120.0, await BpmFromBeats(beats));
    }

    [Fact]
    public async Task MissedBeat_IsTrimmed_NotDraggingTempo()
    {
        var gaps = Repeat([0.34, 0.34, 0.35], 40).ToList();
        gaps[20] = 0.68; // one dropped beat → a doubled gap
        double bpm = await BpmFromBeats(BeatsFromGaps(gaps));
        Assert.InRange(bpm, 174.0, 175.5);
    }

    [Fact]
    public async Task TooFewBeats_ReturnsZero()
    {
        Assert.Equal(0.0, await BpmFromBeats([0.0, 0.5]));
    }
}
