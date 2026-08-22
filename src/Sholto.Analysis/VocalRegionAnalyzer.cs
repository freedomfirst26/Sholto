namespace Sholto.Analysis;

/// <summary>
/// Turns the isolated vocal stem's layer into a few contiguous "vocals present"
/// regions. Feed it the vocal stem's <see cref="WaveformPeaks"/> (from
/// <see cref="StemPeaks.Vocals"/>); it returns the spans the deck paints green.
///
/// Thresholds against the stem's OWN peak envelope so a quiet-but-present vocal
/// still registers, drops brief specks, and bridges short gaps — so the result
/// reads as clean rectangles rather than a stipple.
/// </summary>
public static class VocalRegionAnalyzer
{
    public const float PresenceThreshold = 0.12f; // fraction of the stem's peak envelope
    public const double MinSpanSec = 0.20;        // ignore blips shorter than this
    public const double MergeGapSec = 0.25;       // bridge gaps shorter than this

    public static VocalRegions Analyze(WaveformPeaks vocal, int sampleRate)
    {
        int n = vocal.Max.Length;
        if (n == 0) return VocalRegions.Empty;
        double spp = vocal.SamplesPerPeak / (double)sampleRate;

        float maxE = 0f;
        var energy = new float[n];
        for (int i = 0; i < n; i++)
        {
            float e = MathF.Max(MathF.Abs(vocal.Max[i]), MathF.Abs(vocal.Min[i]));
            energy[i] = e;
            if (e > maxE) maxE = e;
        }
        if (maxE <= 0f) return VocalRegions.Empty;

        float thr = maxE * PresenceThreshold;
        var spans = new List<VocalRegion>();
        int runStart = -1;
        for (int i = 0; i < n; i++)
        {
            bool present = energy[i] > thr;
            if (present && runStart < 0) runStart = i;
            else if (!present && runStart >= 0) { spans.Add(new VocalRegion(runStart * spp, i * spp)); runStart = -1; }
        }
        if (runStart >= 0) spans.Add(new VocalRegion(runStart * spp, n * spp));

        // Bridge short gaps, then drop anything still too short to matter.
        var merged = new List<VocalRegion>();
        foreach (var s in spans)
        {
            if (merged.Count > 0 && s.StartSec - merged[^1].EndSec <= MergeGapSec)
                merged[^1] = new VocalRegion(merged[^1].StartSec, s.EndSec);
            else
                merged.Add(s);
        }
        merged.RemoveAll(s => s.EndSec - s.StartSec < MinSpanSec);
        return new VocalRegions(merged);
    }
}
