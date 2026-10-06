using Sholto.App.Analysis.Analyzers.Waveform;
using Sholto.Data;

namespace Sholto.Interface.MainUI.Tests.WaveformStyles;

/// <summary>Deterministic synthetic peaks for the waveform-style bake tests: a kick every 48
/// columns (bass with a decaying tail), a slow mid swell, and seeded hi-hat noise. Same input
/// every run, so a bake's pixels can be pinned by hash.</summary>
internal sealed class TestWaveformPeaks
{
    public WaveformPeaks Create(int width = 1200)
    {
        var low = new float[width];
        var mid = new float[width];
        var high = new float[width];
        var min = new float[width];
        var max = new float[width];
        uint seed = 12345;
        for (int i = 0; i < width; i++)
        {
            seed = seed * 1664525u + 1013904223u;
            float noise = (seed >> 8) / (float)(1 << 24);
            int sinceKick = i % 48;
            low[i] = sinceKick < 2 ? 1f : MathF.Pow(0.9f, sinceKick);
            mid[i] = 0.3f + 0.25f * MathF.Sin(i / 90f);
            high[i] = (i / 300) % 2 == 0 ? 0.1f * noise : 0.6f * noise;
            max[i] = MathF.Min(1f, (low[i] + mid[i] + high[i]) / 2f);
            min[i] = -max[i];
        }
        return new WaveformPeaks(min, max, low, mid, high, 512, 48000);
    }

    /// <summary>Peaks with no band data (Low/Mid/High empty), which takes the mono-envelope path.</summary>
    public WaveformPeaks CreateWithoutBands(int width = 1200)
    {
        var full = Create(width);
        return full with { Low = [], Mid = [], High = [] };
    }
}
