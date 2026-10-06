using Sholto.App.Analysis.Analyzers.Beats;
using Sholto.App.Analysis.Analyzers.Waveform;
using Sholto.Data;

namespace Sholto.App.Analysis.Analyzers.Segments;

/// <summary>Default <see cref="IBarFeatureExtractor"/>. Bar means of the three band peaks,
/// kick presence (a low-band pulse at the start of each beat), bass presence (sustained low
/// level) and spectral change (column-to-column band difference). Pure computation.</summary>
public sealed class BarFeatureExtractor(PhraseSectionOptions options) : IBarFeatureExtractor
{
    private readonly PhraseSectionOptions _options = options;

    public BarFeatures Extract(WaveformPeaks peaks, Beatgrid grid, int sampleRate)
    {
        int cols = peaks.Low.Length;
        if (cols == 0 || peaks.Mid.Length != cols || peaks.High.Length != cols || grid.IsEmpty)
            return Empty();

        double spp = peaks.SamplesPerPeak / (double)sampleRate;
        double trackSec = cols * spp;
        double barSec = grid.BarPeriodSec;
        double first = grid.FirstDownbeatSec;
        int bars = (int)Math.Floor((trackSec - first) / barSec + 1e-3);
        if (bars < 1) return Empty();

        int Col(double sec) => Math.Clamp((int)Math.Round(sec / spp), 0, cols);

        var low = new float[bars]; var mid = new float[bars]; var high = new float[bars]; var flux = new float[bars];
        for (int b = 0; b < bars; b++)
        {
            int c0 = Col(grid.DownbeatAt(b)), c1 = Math.Max(c0 + 1, Col(grid.DownbeatAt(b + 1)));
            c1 = Math.Min(c1, cols);
            if (c0 >= c1) continue;
            float l = 0, m = 0, h = 0, f = 0;
            for (int c = c0; c < c1; c++)
            {
                l += peaks.Low[c]; m += peaks.Mid[c]; h += peaks.High[c];
                if (c > c0) f += MathF.Abs(peaks.Low[c] - peaks.Low[c - 1]) + MathF.Abs(peaks.Mid[c] - peaks.Mid[c - 1])
                    + MathF.Abs(peaks.High[c] - peaks.High[c - 1]);
            }
            int n = c1 - c0;
            low[b] = l / n; mid[b] = m / n; high[b] = h / n; flux[b] = f / n;
        }

        float lowRef = Sustained(low), midRef = Sustained(mid), highRef = Sustained(high);
        float fluxRef = Percentile(flux, 0.9f);
        var kick = new float[bars]; var bass = new float[bars];
        for (int b = 0; b < bars; b++)
        {
            kick[b] = KickShare(peaks.Low, grid, b, spp, cols, lowRef);
            low[b] /= lowRef; mid[b] /= midRef; high[b] /= highRef;
            flux[b] = fluxRef > 0 ? flux[b] / fluxRef : 0f;
            bass[b] = Math.Clamp((low[b] - _options.BassFloor) / Math.Max(_options.BassRamp, 1e-6f), 0f, 1f);
        }
        return new BarFeatures(low, mid, high, kick, bass, flux);
    }

    private BarFeatures Empty() => new([], [], [], [], [], []);

    private float KickShare(float[] lowBand, Beatgrid grid, int bar, double spp, int cols, float lowRef)
    {
        int beats = grid.BeatsPerBar, kicks = 0;
        for (int k = 0; k < beats; k++)
        {
            double t0 = grid.DownbeatAt(bar) + k * grid.BeatPeriodSec;
            int c0 = Math.Clamp((int)Math.Round(t0 / spp), 0, cols);
            int c1 = Math.Clamp((int)Math.Round((t0 + grid.BeatPeriodSec) / spp), c0, cols);
            int cHead = Math.Min(c1, c0 + Math.Max(1, (int)Math.Round((c1 - c0) * _options.KickHeadFraction)));
            if (cHead >= c1) continue;
            float head = 0, tail = float.MaxValue;
            for (int c = c0; c < cHead; c++) head = Math.Max(head, lowBand[c]);
            for (int c = cHead; c < c1; c++) tail = Math.Min(tail, lowBand[c]);
            if (head / lowRef >= _options.KickFloor && (head - tail) / head >= _options.KickDepth) kicks++;
        }
        return kicks / (float)beats;
    }

    /// <summary>90th percentile of the rolling mean: the level the loud parts sustain, not a one-bar spike.</summary>
    private float Sustained(float[] a)
    {
        int w = Math.Max(1, _options.SustainBars);
        var rolling = new float[a.Length];
        for (int i = 0; i < a.Length; i++)
        {
            int lo = Math.Max(0, i - w / 2), hi = Math.Min(a.Length, lo + w);
            float s = 0; for (int j = lo; j < hi; j++) s += a[j];
            rolling[i] = s / (hi - lo);
        }
        float p = Percentile(rolling, 0.9f);
        return p > 1e-6f ? p : 1f;
    }

    private float Percentile(float[] a, float p)
    {
        if (a.Length == 0) return 0;
        var copy = (float[])a.Clone();
        Array.Sort(copy);
        return copy[Math.Clamp((int)(p * (copy.Length - 1)), 0, copy.Length - 1)];
    }
}
