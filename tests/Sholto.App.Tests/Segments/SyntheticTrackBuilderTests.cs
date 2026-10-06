using Sholto.App.Analysis.Analyzers.Segments;

namespace Sholto.App.Tests.Segments;

public sealed class SyntheticTrackBuilderTests
{
    private const double Bpm = 128;

    private SyntheticTrack Standard(Action<SyntheticTrackBuilder>? configure = null)
    {
        var b = new SyntheticTrackBuilder(Bpm);
        configure?.Invoke(b);
        return b.AddStandardStructure().Build();
    }

    private SyntheticBandLevels Mean(SyntheticTrack t, SongSection s)
    {
        var p = t.Analysis.Peaks;
        double secPerCol = p.SamplesPerPeak / 48000.0;
        int from = (int)(s.StartSec(t.Grid) / secPerCol) + 2;
        int to = (int)(s.EndSec(t.Grid) / secPerCol) - 2;
        float lo = 0, mi = 0, hi = 0;
        for (int i = from; i < to; i++) { lo += p.Low[i]; mi += p.Mid[i]; hi += p.High[i]; }
        int n = to - from;
        return new SyntheticBandLevels(lo / n, mi / n, hi / n);
    }

    private SongSection First(SyntheticTrack t, SectionKind k) => t.Sections.First(s => s.Kind == k);

    [Fact]
    public void StandardStructure_Has128Bars_AndOneDownbeatPerBar()
    {
        var t = Standard();
        Assert.Equal(128, t.TotalBars);
        Assert.Equal(128, t.Analysis.DownbeatTimes.Length);
        Assert.Equal(128 * 4, t.Analysis.BeatTimes.Length);
    }

    [Fact]
    public void Downbeats_AreOneBarApart()
    {
        var t = Standard();
        double bar = 4 * 60.0 / Bpm;
        var d = t.Analysis.DownbeatTimes;
        for (int i = 1; i < d.Length; i++) Assert.Equal(bar, d[i] - d[i - 1], 6);
    }

    [Fact]
    public void Sections_AreContiguousAndMatchRequestedBars()
    {
        var t = Standard();
        Assert.Equal([16, 16, 32, 16, 32, 16], t.Sections.Select(s => s.Bars));
        for (int i = 1; i < t.Sections.Count; i++) Assert.Equal(t.Sections[i - 1].EndBar, t.Sections[i].StartBar);
    }

    [Fact]
    public void SectionTimes_FollowTheGrid()
    {
        var t = Standard();
        var drop = First(t, SectionKind.Drop);
        Assert.Equal(t.Analysis.DownbeatTimes[drop.StartBar], drop.StartSec(t.Grid), 6);
        Assert.Equal(t.Analysis.DownbeatTimes[drop.EndBar], drop.EndSec(t.Grid), 6);
    }

    [Fact]
    public void Peaks_AreAllSameLength_AndCoverTheTrack()
    {
        var p = Standard().Analysis.Peaks;
        Assert.Equal(p.Low.Length, p.High.Length);
        Assert.Equal(p.Low.Length, p.Mid.Length);
        Assert.Equal(p.Low.Length, p.Max.Length);
        Assert.Equal(128 * 4 * 60.0 / Bpm, p.Low.Length * p.SamplesPerPeak / 48000.0, 1);
    }

    [Fact]
    public void Intro_IsQuietKickOnly()
    {
        var t = Standard();
        var m = Mean(t, First(t, SectionKind.Intro));
        Assert.InRange(m.Low, 0.3, 0.6);
        Assert.True(m.Mid < 0.15 && m.High < 0.2);
    }

    [Fact]
    public void Build_HighsRiseAcrossTheSection()
    {
        var t = Standard();
        var b = First(t, SectionKind.Build);
        var h = t.Analysis.Peaks.High;
        double spc = t.Analysis.Peaks.SamplesPerPeak / 48000.0;
        int start = (int)(b.StartSec(t.Grid) / spc), len = (int)((b.EndSec(t.Grid) - b.StartSec(t.Grid)) / spc);
        float early = h[start + len / 10], late = h[start + len * 9 / 10];
        Assert.True(late > early + 0.3f);
        Assert.True(Mean(t, b).Low > 0.3);
    }

    [Fact]
    public void Drop_HasKickBassAndHighsAtMaximum()
    {
        var t = Standard();
        var m = Mean(t, First(t, SectionKind.Drop));
        Assert.True(m.Low > 0.85 && m.Mid > 0.8 && m.High > 0.95);
        Assert.Equal(1.0f, t.Analysis.Peaks.Max.Max());
    }

    [Fact]
    public void Breakdown_HasNoKickOrBass_JustPads()
    {
        var t = Standard();
        var m = Mean(t, First(t, SectionKind.Breakdown));
        Assert.True(m.Low < 0.05);
        Assert.True(m.Mid > 0.4);
    }

    [Fact]
    public void Outro_TailsOff()
    {
        var t = Standard();
        var o = First(t, SectionKind.Outro);
        var p = t.Analysis.Peaks;
        double spc = p.SamplesPerPeak / 48000.0;
        int start = (int)(o.StartSec(t.Grid) / spc), len = (int)((o.EndSec(t.Grid) - o.StartSec(t.Grid)) / spc);
        Assert.True(p.Low[start + len / 10] > p.Low[start + len * 9 / 10] + 0.2f);
        Assert.True(Mean(t, o).Low < Mean(t, First(t, SectionKind.Drop)).Low);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public void LeadIn_ShiftsSectionsAndPhase_AndIsSilent(int leadIn)
    {
        var t = Standard(b => b.WithLeadIn(leadIn));
        Assert.Equal(128 + leadIn, t.TotalBars);
        Assert.Equal(128 + leadIn, t.Analysis.DownbeatTimes.Length);
        Assert.Equal(leadIn, t.Sections[0].StartBar);
        Assert.Equal(leadIn, t.Phrases.PhaseBar);
        Assert.True(t.Phrases.IsPhraseLine(t.Sections[2].StartBar));
        Assert.All(t.Analysis.Peaks.Max.Take(40), v => Assert.Equal(0f, v));
    }

    [Fact]
    public void LoudBuild_IsNearlyAsLoudAsDrop_ButDropIsStillLouder()
    {
        var t = Standard(b => b.WithLoudBuild());
        var build = Mean(t, First(t, SectionKind.Build));
        var drop = Mean(t, First(t, SectionKind.Drop));
        Assert.True(build.Low > 0.75 && build.Mid > 0.75);
        Assert.True(drop.Low > build.Low && drop.Mid > build.Mid && drop.High > build.High);
        Assert.True(drop.Low + drop.Mid + drop.High - (build.Low + build.Mid + build.High) < 0.9);
    }
}
