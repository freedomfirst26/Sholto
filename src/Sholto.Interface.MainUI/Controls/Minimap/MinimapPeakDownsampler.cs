using Sholto.Data;

namespace Sholto.Interface.MainUI.Controls.Minimap;

public sealed class MinimapPeakDownsampler : IMinimapPeakDownsampler
{
    public WaveformPeaks Downsample(WaveformPeaks peaks, int columns)
    {
        int n = peaks.Min.Length;
        if (columns <= 0 || n == 0) return new WaveformPeaks([], [], [], [], [], peaks.SamplesPerPeak, peaks.SampleRate);

        bool hasBands = peaks.Low.Length == n && peaks.Mid.Length == n && peaks.High.Length == n;
        var min = new float[columns];
        var max = new float[columns];
        var low = hasBands ? new float[columns] : [];
        var mid = hasBands ? new float[columns] : [];
        var high = hasBands ? new float[columns] : [];

        for (int x = 0; x < columns; x++)
        {
            int c0 = (int)((long)x * n / columns);
            int c1 = (int)((long)(x + 1) * n / columns);
            if (c1 <= c0) c1 = c0 + 1;
            if (c1 > n) { c1 = n; c0 = n - 1; }

            float lo = float.MaxValue, hi = float.MinValue;
            float sl = 0, sm = 0, sh = 0, xl = 0, xm = 0, xh = 0;
            for (int c = c0; c < c1; c++)
            {
                if (peaks.Min[c] < lo) lo = peaks.Min[c];
                if (peaks.Max[c] > hi) hi = peaks.Max[c];
                if (!hasBands) continue;
                sl += peaks.Low[c]; sm += peaks.Mid[c]; sh += peaks.High[c];
                if (peaks.Low[c] > xl) xl = peaks.Low[c];
                if (peaks.Mid[c] > xm) xm = peaks.Mid[c];
                if (peaks.High[c] > xh) xh = peaks.High[c];
            }
            min[x] = lo;
            max[x] = hi;
            if (!hasBands) continue;
            float count = c1 - c0;
            low[x] = (sl / count + xl) / 2f;
            mid[x] = (sm / count + xm) / 2f;
            high[x] = (sh / count + xh) / 2f;
        }

        int samplesPerPeak = (int)Math.Max(1, Math.Round(peaks.SamplesPerPeak * (double)n / columns));
        return new WaveformPeaks(min, max, low, mid, high, samplesPerPeak, peaks.SampleRate);
    }
}
