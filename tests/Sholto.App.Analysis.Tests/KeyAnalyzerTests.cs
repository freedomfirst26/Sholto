using Sholto.App.Analysis.Analyzers.Keys;
using Sholto.App.Analysis.Harmony;
using Sholto.App.Analysis.Reporting;

namespace Sholto.App.Analysis.Tests;

public class KeyAnalyzerTests
{
    private readonly KeyAnalyzer _analyzer = new();
    private readonly IAnalysisReporter _reporter = new AnalysisReporter(Array.Empty<string>());

    /// <summary>A sustained C major triad (C4, E4, G4), stereo, at 48 kHz.</summary>
    private static DecodedTrack CMajorChord(double seconds)
    {
        const int rate = 48000;
        var samples = new float[(int)(seconds * rate) * 2];
        double[] freqs = [261.63, 329.63, 392.00];
        for (int i = 0; i < samples.Length / 2; i++)
        {
            double v = 0;
            foreach (var f in freqs) v += Math.Sin(2 * Math.PI * f * i / rate);
            samples[2 * i] = samples[2 * i + 1] = (float)(v / 4);
        }
        return new DecodedTrack("/music/chord.wav", samples, rate, 2);
    }

    /// <summary>Pins today's answer for a fixed signal so a change to how the work is split across threads
    /// cannot move it. A bare triad has no tonal context, so the profile match lands on E minor, not C major.</summary>
    [Fact]
    public async Task A_fixed_signal_keeps_the_key_it_has_always_had()
    {
        var key = (await _analyzer.AnalyzeAsync(CMajorChord(20), _reporter)).Key;

        Assert.Equal(new Key(4, false), key);
    }

    [Fact]
    public async Task The_result_does_not_depend_on_how_the_work_is_scheduled()
    {
        var track = CMajorChord(20);

        var runs = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => _analyzer.AnalyzeAsync(track, _reporter)));

        Assert.All(runs, r => Assert.Equal(runs[0].Key, r.Key));
    }
}
