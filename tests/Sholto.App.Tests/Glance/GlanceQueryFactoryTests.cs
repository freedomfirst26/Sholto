using Sholto.App.Glance;

namespace Sholto.App.Tests.Glance;

public class GlanceQueryFactoryTests
{
    private readonly GlanceQueryFactory _factory = new();

    [Fact]
    public void Single_bpm_is_plus_minus_half()
    {
        GlanceQuery q = _factory.Create("bpm:128");
        Assert.Equal(127.5, q.BpmMin);
        Assert.Equal(128.5, q.BpmMax);
    }

    [Fact]
    public void Bpm_range_is_inclusive()
    {
        GlanceQuery q = _factory.Create("BPM:128-124");
        Assert.Equal(124, q.BpmMin);
        Assert.Equal(128, q.BpmMax);
    }

    [Fact]
    public void Reversed_bpm_range_is_labelled_low_to_high()
    {
        GlanceQuery q = _factory.Create("bpm:128-124");
        Assert.Equal(new[] { "BPM 124–128" }, q.FilterChips);
        Assert.Equal(124, q.BpmMin);
        Assert.Equal(128, q.BpmMax);
    }

    [Theory]
    [InlineData("key:13A")]
    [InlineData("key:0A")]
    [InlineData("key:99B")]
    [InlineData("key:Am")]
    public void Key_outside_the_camelot_wheel_is_a_plain_word(string text)
    {
        GlanceQuery q = _factory.Create(text);
        Assert.Null(q.Key);
        Assert.Empty(q.FilterChips);
        Assert.Equal(new[] { text.ToLowerInvariant() }, q.Words);
    }

    [Theory]
    [InlineData("key:1A", "1A")]
    [InlineData("key:9b", "9B")]
    [InlineData("key:10A", "10A")]
    [InlineData("key:12B", "12B")]
    public void Key_on_the_camelot_wheel_is_a_key_filter(string text, string key)
    {
        GlanceQuery q = _factory.Create(text);
        Assert.Equal(key, q.Key);
        Assert.Equal(new[] { "KEY " + key }, q.FilterChips);
        Assert.Empty(q.Words);
    }

    [Fact] public void Key_is_upper_cased() => Assert.Equal("8A", _factory.Create("key:8a").Key);
    [Fact] public void Hash_word_is_a_lower_case_tag() => Assert.Equal(new[] { "garage" }, _factory.Create("#Garage").Tags);
    [Fact] public void Plain_words_are_lower_cased_words() => Assert.Equal(new[] { "rob", "blo" }, _factory.Create("  Rob   BLO ").Words);

    [Fact]
    public void Chips_follow_bpm_key_tags_and_skip_words()
    {
        GlanceQuery q = _factory.Create("bpm:124-128 key:8a #techno x");
        Assert.Equal(new[] { "BPM 124–128", "KEY 8A", "#techno" }, q.FilterChips);
        Assert.Equal(new[] { "x" }, q.Words);
    }

    [Fact]
    public void Empty_query_has_no_constraints()
    {
        GlanceQuery q = _factory.Create("");
        Assert.Empty(q.Words);
        Assert.Empty(q.FilterChips);
        Assert.Null(q.BpmMin);
        Assert.Null(q.Key);
    }
}
