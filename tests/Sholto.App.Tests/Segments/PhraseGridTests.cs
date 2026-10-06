using Sholto.App.Analysis.Analyzers.Segments;

namespace Sholto.App.Tests.Segments;

public sealed class PhraseGridTests
{
    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(0, 8, true)]
    [InlineData(0, 4, false)]
    [InlineData(1, 0, false)]
    [InlineData(1, 1, true)]
    [InlineData(1, 9, true)]
    [InlineData(4, 4, true)]
    [InlineData(4, 12, true)]
    [InlineData(4, 8, false)]
    [InlineData(4, 0, false)]
    public void IsPhraseLine_CountsFromPhaseBar(int phase, int bar, bool expected)
        => Assert.Equal(expected, new PhraseGrid(phase).IsPhraseLine(bar));

    [Fact]
    public void LineWeight_PhaseZero_Is8_16_32()
    {
        var g = new PhraseGrid(0);
        Assert.Equal(32, g.LineWeight(0));
        Assert.Equal(8, g.LineWeight(8));
        Assert.Equal(16, g.LineWeight(16));
        Assert.Equal(8, g.LineWeight(24));
        Assert.Equal(32, g.LineWeight(32));
        Assert.Equal(0, g.LineWeight(4));
    }

    [Fact]
    public void LineWeight_IsCountedFromPhaseBar()
    {
        var g = new PhraseGrid(4);
        Assert.Equal(32, g.LineWeight(4));
        Assert.Equal(8, g.LineWeight(12));
        Assert.Equal(16, g.LineWeight(20));
        Assert.Equal(32, g.LineWeight(36));
        Assert.Equal(0, g.LineWeight(0));
        Assert.Equal(0, g.LineWeight(32));
    }

    [Fact]
    public void BarsBeforePhaseBar_AreNotPhraseLines()
    {
        var g = new PhraseGrid(4);
        Assert.False(g.IsPhraseLine(0));
        Assert.False(g.IsPhraseLine(3));
        Assert.Equal(0, g.LineWeight(0));
    }
}
