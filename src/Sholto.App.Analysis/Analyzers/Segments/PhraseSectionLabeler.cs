namespace Sholto.App.Analysis.Analyzers.Segments;

/// <summary>Default <see cref="IPhraseSectionLabeler"/>. Two modes. If the track has real
/// drops (kick and bass on, low and high at the sustained maximum, right after a release,
/// covering no more than <see cref="PhraseSectionOptions.MaxDropShare"/> of the track) it
/// labels Drop / Build / Breakdown / Intro / Outro around them. Otherwise it never says Drop:
/// repeating blocks become Verse / Chorus by relative loudness, the rest neutral Section.</summary>
public sealed class PhraseSectionLabeler(PhraseSectionOptions options) : IPhraseSectionLabeler
{
    private readonly PhraseSectionOptions _options = options;

    private readonly record struct Stats(float Low, float Mid, float High, float Kick, float Bass, float HighRise, float Loudness);

    public IReadOnlyList<SongSection> Label(BarFeatures f, IReadOnlyList<SongSection> spans)
    {
        int n = spans.Count;
        if (n == 0) return [];
        var stats = spans.Select(s => StatsOf(f, s)).ToArray();
        var kinds = new SectionKind[n];

        bool[] drop = FindDrops(stats, spans, out bool[] loudRun);
        int audible = spans.Sum(s => s.Bars);
        int dropBars = Enumerable.Range(0, n).Where(i => drop[i]).Sum(i => spans[i].Bars);
        bool dropMode = dropBars > 0 && dropBars <= _options.MaxDropShare * audible;

        if (dropMode) LabelAroundDrops(stats, drop, loudRun, kinds);
        else LabelWithoutDrops(stats, kinds);

        return spans.Select((s, i) => new SongSection(kinds[i], s.StartBar, s.Bars)).ToArray();
    }

    private Stats StatsOf(BarFeatures f, SongSection s)
    {
        float Mean(float[] a, int from, int to)
        {
            if (to <= from) return 0;
            float sum = 0; for (int i = from; i < to; i++) sum += a[i];
            return sum / (to - from);
        }
        int a0 = s.StartBar, a1 = Math.Min(s.EndBar, f.Bars);
        int third = Math.Max(1, (a1 - a0) / 3);
        float rise = Mean(f.High, a1 - third, a1) - Mean(f.High, a0, a0 + third);
        float low = Mean(f.Low, a0, a1), mid = Mean(f.Mid, a0, a1), high = Mean(f.High, a0, a1);
        return new Stats(low, mid, high, Mean(f.Kick, a0, a1), Mean(f.Bass, a0, a1), rise, (low + mid + high) / 3f);
    }

    /// <summary>Candidate drops grouped into runs; a run is a Drop only if a release precedes it.
    /// <paramref name="loudRun"/> marks candidates that failed the release test.</summary>
    private bool[] FindDrops(Stats[] st, IReadOnlyList<SongSection> spans, out bool[] loudRun)
    {
        int n = st.Length;
        var cand = new bool[n];
        for (int i = 0; i < n; i++)
            cand[i] = st[i].Kick >= _options.DropKickMin && st[i].Bass >= _options.DropBassMin
                && st[i].Low >= _options.DropLowLevel && st[i].High >= _options.DropHighLevel
                && !(_options.RisingExcludesDrop && st[i].HighRise >= _options.RisingHigh);
        var drop = new bool[n];
        loudRun = new bool[n];
        for (int i = 0; i < n;)
        {
            if (!cand[i]) { i++; continue; }
            int j = i; while (j < n && cand[j]) j++;
            bool release = i > 0 && (st[i - 1].Kick < _options.ReleaseMax || st[i - 1].Bass < _options.ReleaseMax
                || st[i - 1].HighRise >= _options.RisingHigh);
            for (int k = i; k < j; k++) { drop[k] = release; loudRun[k] = !release; }
            i = j;
        }
        return drop;
    }

    private void LabelAroundDrops(Stats[] st, bool[] drop, bool[] loudRun, SectionKind[] kinds)
    {
        int n = st.Length;
        bool dropBefore = false;
        for (int i = 0; i < n; i++)
        {
            bool nextDrop = i + 1 < n && drop[i + 1];
            if (drop[i]) { kinds[i] = SectionKind.Drop; dropBefore = true; continue; }
            if (loudRun[i]) { kinds[i] = SectionKind.Chorus; continue; }

            bool building = st[i].Kick >= _options.BuildKickMin || st[i].HighRise >= _options.RisingHigh;
            if (i == 0)
                kinds[i] = nextDrop && st[i].HighRise >= _options.RisingHigh ? SectionKind.Build : SectionKind.Intro;
            else if (i == n - 1)
                kinds[i] = SectionKind.Outro;
            else if (nextDrop && building)
                kinds[i] = SectionKind.Build;
            else if (dropBefore && (st[i].Kick < _options.BuildKickMin || st[i].Bass < _options.ReleaseMax))
                kinds[i] = SectionKind.Breakdown;
            else
                kinds[i] = SectionKind.Section;
        }
    }

    private void LabelWithoutDrops(Stats[] st, SectionKind[] kinds)
    {
        int n = st.Length;
        Array.Fill(kinds, SectionKind.Section);
        if (n < 2) return;

        float loudest = st.Max(s => s.Loudness);
        int lo = 0, hi = n;
        if (st[0].Loudness < _options.IntroOutroLevel * loudest) { kinds[0] = SectionKind.Intro; lo = 1; }
        if (n > 2 && st[n - 1].Loudness < _options.IntroOutroLevel * loudest) { kinds[n - 1] = SectionKind.Outro; hi = n - 1; }

        // Greedy clustering of the remaining sections by feature distance.
        var cluster = new int[n]; Array.Fill(cluster, -1);
        var heads = new List<int>();
        for (int i = lo; i < hi; i++)
        {
            int found = heads.FindIndex(h => Distance(st[h], st[i]) <= _options.RepeatDistance);
            if (found < 0) { heads.Add(i); found = heads.Count - 1; }
            cluster[i] = found;
        }
        var repeating = Enumerable.Range(0, heads.Count)
            .Where(c => Enumerable.Range(lo, hi - lo).Count(i => cluster[i] == c) >= 2).ToList();
        if (repeating.Count == 0) return;

        float Loud(int c) => Enumerable.Range(lo, hi - lo).Where(i => cluster[i] == c).Average(i => st[i].Loudness);
        int chorus = repeating.OrderByDescending(Loud).First();
        float median = st.Skip(lo).Take(hi - lo).Select(s => s.Loudness).OrderBy(x => x).ElementAt((hi - lo) / 2);
        for (int i = lo; i < hi; i++)
        {
            if (!repeating.Contains(cluster[i])) continue;
            bool isChorus = cluster[i] == chorus && (repeating.Count > 1 || Loud(chorus) >= median);
            kinds[i] = isChorus ? SectionKind.Chorus : SectionKind.Verse;
        }
    }

    private float Distance(Stats a, Stats b) => new[]
    {
        MathF.Abs(a.Low - b.Low), MathF.Abs(a.Mid - b.Mid), MathF.Abs(a.High - b.High),
        MathF.Abs(a.Kick - b.Kick), MathF.Abs(a.Bass - b.Bass),
    }.Max();
}
