namespace Sholto.Analysis;

/// <summary>
/// Coarse structural segmentation from the energy envelope aligned to the beatgrid.
/// For each bar (between consecutive downbeats) we take the band energy from the
/// existing <see cref="WaveformPeaks"/>, normalise it, quantise into low/mid/high
/// tiers, merge neighbouring same-tier bars into sections, and slap on a heuristic
/// label from the energy shape (opening-low = Intro, sustained-high-with-bass = Drop,
/// low-dip-between-highs = Breakdown, trailing-low = Outro, rising = BuildUp).
///
/// This is deliberately cheap and honest — no trained model — so labels are a hint
/// for the minimap, not ground truth. It reuses peaks that are already computed.
/// </summary>
public static class SongSegmentAnalyzer
{
    private const int MinBars = 4;              // don't emit sections shorter than this
    private const float LowTier = 0.35f;        // normalised-energy tier cuts
    private const float HighTier = 0.70f;

    public static SongSegments Analyze(WaveformPeaks peaks, double[] downbeats, int sampleRate)
    {
        int cols = peaks.Min.Length;
        if (cols == 0 || downbeats is null || downbeats.Length < 3) return SongSegments.Empty;

        double spp = peaks.SamplesPerPeak / (double)sampleRate;
        bool hasBands = peaks.Low.Length == cols;
        int bars = downbeats.Length - 1;

        var energy = new float[bars];
        var bass = new float[bars];
        for (int b = 0; b < bars; b++)
        {
            int c0 = Math.Clamp((int)(downbeats[b] / spp), 0, cols);
            int c1 = Math.Clamp((int)(downbeats[b + 1] / spp), c0 + 1, cols);
            float sum = 0, low = 0;
            for (int c = c0; c < c1; c++)
            {
                sum += hasBands ? peaks.Low[c] + peaks.Mid[c] + peaks.High[c]
                                : MathF.Max(MathF.Abs(peaks.Max[c]), MathF.Abs(peaks.Min[c]));
                if (hasBands) low += peaks.Low[c];
            }
            int n = c1 - c0;
            energy[b] = n > 0 ? sum / n : 0;
            bass[b] = n > 0 ? low / n : 0;
        }

        float norm = Percentile(energy, 0.90f);
        if (norm <= 0) norm = 1;
        for (int b = 0; b < bars; b++) energy[b] = Math.Clamp(energy[b] / norm, 0f, 1f);
        float bassNorm = Percentile(bass, 0.90f);
        if (bassNorm <= 0) bassNorm = 1;

        var smooth = Smooth(energy);

        // Runs of equal tier.
        var runs = new List<(int Start, int End, int Tier)>();
        int runStart = 0, runTier = Tier(smooth[0]);
        for (int b = 1; b < bars; b++)
        {
            int t = Tier(smooth[b]);
            if (t != runTier) { runs.Add((runStart, b, runTier)); runStart = b; runTier = t; }
        }
        runs.Add((runStart, bars, runTier));

        // Merge runs shorter than MinBars into the previous (or next for the first).
        var merged = new List<(int Start, int End, int Tier)>();
        foreach (var r in runs)
        {
            if (r.End - r.Start < MinBars && merged.Count > 0)
                merged[^1] = (merged[^1].Start, r.End, merged[^1].Tier);
            else
                merged.Add(r);
        }

        // Label from the energy shape.
        var segs = new List<SongSegment>();
        for (int i = 0; i < merged.Count; i++)
        {
            var (start, end, tier) = merged[i];
            float e = Avg(smooth, start, end);
            float b = Avg(bass, start, end) / bassNorm;
            int prevTier = i > 0 ? merged[i - 1].Tier : -1;
            int nextTier = i < merged.Count - 1 ? merged[i + 1].Tier : -1;
            bool first = i == 0, last = i == merged.Count - 1;

            SegmentKind kind =
                first && tier <= 1                     ? SegmentKind.Intro :
                last && tier <= 1                      ? SegmentKind.Outro :
                tier == 2 && b >= 0.6f                  ? SegmentKind.Drop :
                tier == 2                               ? SegmentKind.Chorus :
                tier == 0 && prevTier == 2 && nextTier == 2 ? SegmentKind.Breakdown :
                tier == 1 && nextTier == 2 && prevTier <= 1 ? SegmentKind.BuildUp :
                tier == 1                               ? SegmentKind.Verse :
                                                          SegmentKind.Bridge;

            segs.Add(new SongSegment(downbeats[start], downbeats[Math.Min(end, downbeats.Length - 1)], kind, e));
        }
        return new SongSegments(segs);
    }

    private static int Tier(float v) => v < LowTier ? 0 : v < HighTier ? 1 : 2;

    private static float[] Smooth(float[] a)
    {
        if (a.Length < 3) return a;
        var s = new float[a.Length];
        for (int i = 0; i < a.Length; i++)
        {
            int lo = Math.Max(0, i - 1), hi = Math.Min(a.Length - 1, i + 1);
            float sum = 0; for (int j = lo; j <= hi; j++) sum += a[j];
            s[i] = sum / (hi - lo + 1);
        }
        return s;
    }

    private static float Avg(float[] a, int start, int end)
    {
        if (end <= start) return 0;
        float sum = 0; for (int i = start; i < end && i < a.Length; i++) sum += a[i];
        return sum / (end - start);
    }

    private static float Percentile(float[] a, float p)
    {
        if (a.Length == 0) return 0;
        var copy = (float[])a.Clone();
        Array.Sort(copy);
        int idx = Math.Clamp((int)(p * (copy.Length - 1)), 0, copy.Length - 1);
        return copy[idx];
    }
}
