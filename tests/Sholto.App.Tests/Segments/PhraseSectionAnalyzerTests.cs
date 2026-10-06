using Sholto.App.Analysis.Analyzers.Beats;
using Sholto.App.Analysis.Analyzers.Segments;

namespace Sholto.App.Tests.Segments;

public sealed class PhraseSectionAnalyzerTests
{
    private const double Bpm = 128;
    private const int SampleRate = 48000;

    private IPhraseSectionAnalyzer Create()
    {
        var options = new PhraseSectionOptions();
        return new PhraseSectionAnalyzer(new BarFeatureExtractor(options), new PhraseSectionLabeler(options), options);
    }

    private (PhraseGrid Phrases, IReadOnlyList<SongSection> Sections) Run(SyntheticTrack t)
        => Create().Analyze(t.Analysis.Peaks, t.Grid, SampleRate);

    private string Describe(IEnumerable<SongSection> s) => string.Join(" ", s.Select(x => $"{x.Kind}@{x.StartBar}+{x.Bars}"));

    [Fact]
    public void StandardStructure_IsRecoveredExactly()
    {
        var t = new SyntheticTrackBuilder(Bpm).AddStandardStructure().Build();
        var (phrases, sections) = Run(t);
        Assert.Equal(0, phrases.PhaseBar);
        Assert.Equal(Describe(t.Sections), Describe(sections));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public void LeadIn_PhaseIsFound_AndBoundariesLandOnPhrases(int leadIn)
    {
        var t = new SyntheticTrackBuilder(Bpm).WithLeadIn(leadIn).AddStandardStructure().Build();
        var (phrases, sections) = Run(t);
        Assert.Equal(leadIn, phrases.PhaseBar);
        Assert.Equal(Describe(t.Sections), Describe(sections));
        Assert.All(sections, s => Assert.True(phrases.IsPhraseLine(s.StartBar)));
    }

    [Fact]
    public void LoudBuild_ThenLouderDrop_DropStartsAtSecondPhrase()
    {
        var t = new SyntheticTrackBuilder(Bpm).WithLoudBuild().AddStandardStructure().Build();
        var (_, sections) = Run(t);
        var firstDrop = sections.First(s => s.Kind == SectionKind.Drop);
        Assert.Equal(First(t, SectionKind.Drop).StartBar, firstDrop.StartBar);
        Assert.Equal(SectionKind.Build, sections.First(s => s.EndBar == firstDrop.StartBar).Kind);
    }

    [Fact]
    public void FlatLoudSong_IsNotOneDrop()
    {
        var t = new SyntheticTrackBuilder(Bpm).Add(SectionKind.Chorus, 64).Build();
        var (_, sections) = Run(t);
        Assert.DoesNotContain(sections, s => s.Kind == SectionKind.Drop);
        Assert.Equal(SectionKind.Section, Assert.Single(sections).Kind);
    }

    [Fact]
    public void RepeatingVerseChorusSong_HasNoDrops_AndChorusIsTheLouderBlock()
    {
        var b = new SyntheticTrackBuilder(Bpm);
        for (int i = 0; i < 3; i++) b.Add(SectionKind.Verse, 16).Add(SectionKind.Chorus, 16);
        var (_, sections) = Run(b.Build());
        Assert.DoesNotContain(sections, s => s.Kind == SectionKind.Drop);
        Assert.All(sections.Where(s => s.StartBar % 32 == 0), s => Assert.True(s.Kind is SectionKind.Verse or SectionKind.Intro));
        Assert.All(sections.Where(s => s.StartBar % 32 == 16), s => Assert.Equal(SectionKind.Chorus, s.Kind));
    }

    [Fact]
    public void NoBeatgrid_GivesNoSections()
    {
        var t = new SyntheticTrackBuilder(Bpm).AddStandardStructure().Build();
        var (_, sections) = Create().Analyze(t.Analysis.Peaks, new Beatgrid(0, 0, 4, 0), SampleRate);
        Assert.Empty(sections);
    }

    [Fact]
    public void BreakdownWithoutBass_BetweenDrops_IsBreakdown()
    {
        var t = new SyntheticTrackBuilder(Bpm).AddStandardStructure().Build();
        var (_, sections) = Run(t);
        var breakdown = First(t, SectionKind.Breakdown);
        var found = sections.Single(s => s.StartBar == breakdown.StartBar);
        Assert.Equal(SectionKind.Breakdown, found.Kind);
        Assert.Equal(breakdown.Bars, found.Bars);
    }

    private SongSection First(SyntheticTrack t, SectionKind k) => t.Sections.First(s => s.Kind == k);
}
