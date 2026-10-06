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
