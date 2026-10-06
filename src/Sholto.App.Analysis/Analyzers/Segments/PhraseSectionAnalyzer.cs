using Sholto.App.Analysis.Analyzers.Beats;
using Sholto.App.Analysis.Analyzers.Waveform;
using Sholto.Data;

namespace Sholto.App.Analysis.Analyzers.Segments;

/// <summary>
/// Default <see cref="IPhraseSectionAnalyzer"/>. Per-bar features, then novelty (how much the
/// bars just after a bar differ from the bars just before), the phrase phase that best lines
/// the strong change points up on 8-bar lines, change points snapped onto those lines (16 and
/// 32-bar lines reach further), short interior sections folded into a neighbour, and finally
/// labels from <see cref="IPhraseSectionLabeler"/>. Leading silence is trimmed
/// first, so a grid that starts a few bars before the music does not shift the sections.
/// Pure computation; holds no state.
/// </summary>
public sealed class PhraseSectionAnalyzer(
    IBarFeatureExtractor extractor,
    IPhraseSectionLabeler labeler,
    PhraseSectionOptions options) : IPhraseSectionAnalyzer
{
    private readonly IBarFeatureExtractor _extractor = extractor;
    private readonly IPhraseSectionLabeler _labeler = labeler;
    private readonly PhraseSectionOptions _options = options;

    public (PhraseGrid Phrases, IReadOnlyList<SongSection> Sections) Analyze(WaveformPeaks peaks, Beatgrid grid, int sampleRate)
    {
        var none = (new PhraseGrid(0, _options.PhraseBars), (IReadOnlyList<SongSection>)[]);
        if (grid is null || grid.IsEmpty) return none;
        var f = _extractor.Extract(peaks, grid, sampleRate);
        if (f.Bars < 2) return none;

        int start = 0, end = f.Bars;
        while (start < end && f.Loudness(start) < _options.SilenceLevel) start++;
        if (end - start < 2) return none;

        var novelty = Novelty(f, start, end);
        var changes = ChangePoints(novelty, start);
        int phase = FindPhase(changes, start);
        var phrases = new PhraseGrid(phase, _options.PhraseBars);

        var cuts = new SortedSet<int> { start };
        foreach (var (bar, _) in changes)
        {
            int line = Snap(bar, phrases);
            if (line > start && line < end) cuts.Add(line);
        }
        var spans = ToSpans(cuts, end);
        spans = MergeShort(spans, f);
        return (phrases, Coalesce(_labeler.Label(f, spans)));
    }

    /// <summary>Adjacent sections with the same kind are one section.</summary>
    private IReadOnlyList<SongSection> Coalesce(IReadOnlyList<SongSection> labelled)
    {
        var result = new List<SongSection>();
        foreach (var s in labelled)
        {
            if (result.Count > 0 && result[^1].Kind == s.Kind)
                result[^1] = new SongSection(s.Kind, result[^1].StartBar, s.EndBar - result[^1].StartBar);
            else
                result.Add(s);
        }
        return result;
    }

    /// <summary>Weighted distance between the Gaussian-weighted feature means of the bars after
    /// and before each bar, within [start, end). Index 0 is bar <paramref name="start"/>.</summary>
    private float[] Novelty(BarFeatures f, int start, int end)
    {
        int n = end - start, w = _options.NoveltyBars;
        var feats = new[] { f.Low, f.Mid, f.High, f.Kick, f.Bass, f.Flux };
        var weights = _options.NoveltyWeights;
        var kernel = Enumerable.Range(0, w).Select(i => Math.Exp(-Math.Pow(i + 0.5, 2) / (2 * Math.Pow(w / 4.0, 2)))).ToArray();
        var nov = new float[n];
        for (int b = 1; b < n; b++)
        {
            float dist = 0;
            for (int k = 0; k < feats.Length; k++)
            {
                double before = 0, after = 0, wb = 0, wa = 0;
                for (int i = 0; i < w; i++)
                {
                    int l = b - 1 - i, r = b + i;
                    if (l >= 0) { before += kernel[i] * feats[k][start + l]; wb += kernel[i]; }
                    if (r < n) { after += kernel[i] * feats[k][start + r]; wa += kernel[i]; }
                }
                if (wb > 0 && wa > 0) dist += weights[k] * (float)Math.Abs(after / wa - before / wb);
            }
            nov[b] = dist;
        }
        return nov;
    }

    /// <summary>Local maxima of novelty above the strength threshold, as absolute bars.</summary>
    private List<(int Bar, float Strength)> ChangePoints(float[] nov, int start)
    {
        var result = new List<(int, float)>();
        int gap = _options.ChangePeakGapBars;
        for (int b = 1; b < nov.Length; b++)
        {
            if (nov[b] < _options.ChangeMinStrength) continue;
            bool peak = true;
            for (int j = Math.Max(0, b - gap); j <= Math.Min(nov.Length - 1, b + gap) && peak; j++)
                if (j < b ? nov[j] >= nov[b] : j > b && nov[j] > nov[b]) peak = false;
            if (peak) result.Add((start + b, nov[b]));
        }
        return result;
    }

    /// <summary>The offset 0..PhraseBars-1 that gains the most strength: a change point on the
    /// line counts fully, one bar off counts half. Ties go to the lowest offset, and with no
    /// change points the phase is where the music starts.</summary>
    private int FindPhase(List<(int Bar, float Strength)> changes, int start)
    {
        int p = _options.PhraseBars;
        if (changes.Count == 0) return ((start % p) + p) % p;
        int best = 0; float bestScore = -1;
        for (int k = 0; k < p; k++)
        {
            float score = 0;
            foreach (var (bar, strength) in changes)
            {
                int d = Math.Abs(((bar - k) % p + p) % p);
                d = Math.Min(d, p - d);
                score += strength * Math.Max(0f, 1f - d / 2f);
            }
            if (score > bestScore + 1e-6f) { best = k; bestScore = score; }
        }
        return best;
    }

    /// <summary>Nearest phrase line within that line's snap radius (bigger for 16 and 32-bar lines); -1 if none.</summary>
    private int Snap(int bar, PhraseGrid phrases)
    {
        int best = -1, bestDist = int.MaxValue;
        int reach = Math.Max(_options.Snap8Bars, Math.Max(_options.Snap16Bars, _options.Snap32Bars));
        for (int d = -reach; d <= reach; d++)
        {
            int line = bar + d;
            int weight = phrases.LineWeight(line);
            if (weight == 0) continue;
            int radius = weight >= phrases.PhraseBars * 4 ? _options.Snap32Bars
                : weight >= phrases.PhraseBars * 2 ? _options.Snap16Bars : _options.Snap8Bars;
            if (Math.Abs(d) > radius) continue;
            if (Math.Abs(d) < bestDist) { best = line; bestDist = Math.Abs(d); }
        }
        return best;
    }

    private List<SongSection> ToSpans(SortedSet<int> cuts, int end)
    {
        var list = cuts.ToList();
        var spans = new List<SongSection>();
        for (int i = 0; i < list.Count; i++)
        {
            int to = i + 1 < list.Count ? list[i + 1] : end;
            spans.Add(new SongSection(SectionKind.Section, list[i], to - list[i]));
        }
        return spans;
    }

    /// <summary>Folds interior sections shorter than the minimum into the more similar neighbour.</summary>
    private List<SongSection> MergeShort(List<SongSection> spans, BarFeatures f)
    {
        while (true)
        {
            int idx = -1;
            for (int i = 1; i < spans.Count - 1; i++)
                if (spans[i].Bars < _options.MinSectionBars && (idx < 0 || spans[i].Bars < spans[idx].Bars)) idx = i;
            if (idx < 0) return spans;

            bool intoPrev = Distance(f, spans[idx], spans[idx - 1]) <= Distance(f, spans[idx], spans[idx + 1]);
            int keep = intoPrev ? idx - 1 : idx + 1;
            int a = Math.Min(keep, idx), b = Math.Max(keep, idx);
            var merged = new SongSection(SectionKind.Section, spans[a].StartBar, spans[b].EndBar - spans[a].StartBar);
            spans.RemoveAt(b);
            spans[a] = merged;
        }
    }

    private float Distance(BarFeatures f, SongSection a, SongSection b)
    {
        float[] Mean(SongSection s)
        {
            var m = new float[5];
            for (int i = s.StartBar; i < s.EndBar && i < f.Bars; i++)
            {
                m[0] += f.Low[i]; m[1] += f.Mid[i]; m[2] += f.High[i]; m[3] += f.Kick[i]; m[4] += f.Bass[i];
            }
            for (int k = 0; k < 5; k++) m[k] /= Math.Max(1, s.Bars);
            return m;
        }
        var x = Mean(a); var y = Mean(b);
        float d = 0; for (int k = 0; k < 5; k++) d += MathF.Abs(x[k] - y[k]);
        return d;
    }
}
