using Sholto.App.Analysis.Analyzers.Complement;
using Sholto.App.Analysis.Analyzers.Segments;

namespace Sholto.App.Analysis.Tests.Complement;

public class ComplementOptionsTests
{
    [Fact]
    public void Defaults_are_pinned()
    {
        var options = new ComplementOptions();

        Assert.Equal(0.35, options.KeyWeight);
        Assert.Equal(0.25, options.TempoWeight);
        Assert.Equal(0.2, options.EnergyWeight);
        Assert.Equal(0.2, options.RoleWeight);
        Assert.Equal(1.0, options.PerfectKeyFit);
        Assert.Equal(0.8, options.CloseKeyFit);
        Assert.Equal(0.6, options.EnergyBoostKeyFit);
        Assert.Equal(6.0, options.TempoWindowPercent);
        Assert.Equal(0.05, options.EnergyLift);
        Assert.Equal(0.3, options.EnergyGap);
        Assert.Equal(1.0, options.PairedRoleFit);
        Assert.Equal(0.3, options.UnpairedRoleFit);
    }

    [Fact]
    public void Default_role_pairs_are_pinned()
    {
        var pairs = new ComplementOptions().RolePairs;

        Assert.Equal(Enum.GetValues<SectionKind>().Length, pairs.Count);
        Assert.Equal([SectionKind.Intro, SectionKind.Build], pairs[SectionKind.Intro]);
        Assert.Equal([SectionKind.Build, SectionKind.Drop], pairs[SectionKind.Build]);
        Assert.Equal([SectionKind.Drop, SectionKind.Build], pairs[SectionKind.Drop]);
        Assert.Equal([SectionKind.Build, SectionKind.Breakdown], pairs[SectionKind.Breakdown]);
        Assert.Equal([SectionKind.Verse, SectionKind.Chorus], pairs[SectionKind.Verse]);
        Assert.Equal([SectionKind.Chorus, SectionKind.Verse], pairs[SectionKind.Chorus]);
        Assert.Equal([SectionKind.Chorus, SectionKind.Build], pairs[SectionKind.Bridge]);
        Assert.Equal([SectionKind.Intro, SectionKind.Build], pairs[SectionKind.Outro]);
        Assert.Equal([SectionKind.Section], pairs[SectionKind.Section]);
    }
}
