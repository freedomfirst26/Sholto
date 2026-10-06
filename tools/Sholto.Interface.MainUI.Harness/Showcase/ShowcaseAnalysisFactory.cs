using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis.Analyzers.Waveform;
using Sholto.Interface.MainUI.ViewModels;
using Sholto.Data;

namespace Sholto.Interface.MainUI.Harness.Showcase;

/// <summary>Default <see cref="IShowcaseAnalysisFactory"/>. Repeats the Layout Wizard's demo track (a
/// 256-beat arrangement at the demo tempo, see <see cref="IDemoWaveformFactory"/>) end to end until it
/// covers the requested length, and lays a grid at the demo tempo from 0 s so the kicks sit on the beats.
/// The demo is a whole number of bars, so the repeats join on a downbeat.</summary>
/// <param name="demo">The demo peaks and their tempo.</param>
public sealed class ShowcaseAnalysisFactory(IDemoWaveformFactory demo) : IShowcaseAnalysisFactory
{
    private const int BeatsPerBar = 4;

    private readonly IDemoWaveformFactory _demo = demo;

    public BasicAnalysis Create(double seconds)
    {
        var source = _demo.Peaks;
        int spp = source.SamplesPerPeak;
        int n = (int)Math.Round(seconds * source.SampleRate / spp);
        var peaks = new WaveformPeaks(
            Tile(source.Min, n), Tile(source.Max, n),
            Tile(source.Low, n), Tile(source.Mid, n), Tile(source.High, n), spp, source.SampleRate);

        double beat = 60.0 / _demo.Bpm;
        int beats = (int)Math.Floor(seconds / beat);
        var beatTimes = new double[beats];
        var downbeats = new double[(beats + BeatsPerBar - 1) / BeatsPerBar];
        for (int i = 0; i < beats; i++)
        {
            beatTimes[i] = i * beat;
            if (i % BeatsPerBar == 0) downbeats[i / BeatsPerBar] = i * beat;
        }
        return new BasicAnalysis(peaks, _demo.Bpm, beatTimes, downbeats);
    }

    private float[] Tile(float[]? source, int n)
    {
        var result = new float[n];
        if (source is null || source.Length == 0) return result;
        for (int i = 0; i < n; i++) result[i] = source[i % source.Length];
        return result;
    }
}
