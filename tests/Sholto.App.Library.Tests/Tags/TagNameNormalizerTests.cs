using Sholto.App.Library.Tags;

namespace Sholto.App.Library.Tests.Tags;

public class TagNameNormalizerTests
{
    private readonly TagNameNormalizer _normalizer = new();

    [Theory]
    [InlineData("Deep House", "Deep House")]
    [InlineData("  Deep House  ", "Deep House")]
    [InlineData("Deep   House", "Deep House")]
    [InlineData("Deep\tHouse", "Deep House")]
    [InlineData(" deep  house ", "deep house")]
    public void Normalize_trims_and_collapses_whitespace(string raw, string expected)
    {
        Assert.Equal(expected, _normalizer.Normalize(raw));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\t")]
    [InlineData(null)]
    public void Normalize_returns_null_on_empty_after_trim(string? raw)
    {
        Assert.Null(_normalizer.Normalize(raw));
    }

    [Fact]
    public void Normalize_returns_null_when_longer_than_100_chars()
    {
        Assert.Null(_normalizer.Normalize(new string('x', 101)));
    }

    [Fact]
    public void Normalize_accepts_exactly_100_chars()
    {
        var s = new string('x', 100);
        Assert.Equal(s, _normalizer.Normalize(s));
    }

    [Fact]
    public void Max_tag_length_constant()
    {
        Assert.Equal(100, TagNameNormalizer.MaxLength);
    }
}
