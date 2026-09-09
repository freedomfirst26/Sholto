using Sholto.App.Models;

namespace Sholto.App.Tests;

public class TagRecencyTests
{
    [Fact]
    public void RecentNames_returns_most_recently_used_first()
    {
        var r = new TagRecency();
        r.MarkUsed("techno");
        r.MarkUsed("deep house");
        r.MarkUsed("acid");

        Assert.Equal(new[] { "acid", "deep house", "techno" }, r.RecentNames(10));
    }

    [Fact]
    public void MarkUsed_again_moves_a_tag_back_to_the_front()
    {
        var r = new TagRecency();
        r.MarkUsed("techno");
        r.MarkUsed("acid");
        r.MarkUsed("techno");

        Assert.Equal(new[] { "techno", "acid" }, r.RecentNames(10));
    }

    [Fact]
    public void MarkUsed_is_case_insensitive_and_keeps_one_entry()
    {
        var r = new TagRecency();
        r.MarkUsed("Deep House");
        r.MarkUsed("deep house");

        Assert.Single(r.RecentNames(10));
    }

    [Fact]
    public void OrderRecentFirst_puts_remembered_names_ahead_and_keeps_the_rest_in_order()
    {
        var r = new TagRecency();
        r.MarkUsed("techno");
        r.MarkUsed("acid");

        var incoming = new[] { "ambient", "breaks", "techno", "acid", "dub" };
        var ordered = r.OrderRecentFirst(incoming, s => s);

        Assert.Equal(new[] { "acid", "techno", "ambient", "breaks", "dub" }, ordered);
    }

    [Fact]
    public void OrderRecentFirst_with_an_empty_store_changes_nothing()
    {
        var r = new TagRecency();
        var incoming = new[] { "ambient", "breaks", "dub" };
        Assert.Equal(incoming, r.OrderRecentFirst(incoming, s => s));
    }

    [Fact]
    public void Blank_names_are_ignored()
    {
        var r = new TagRecency();
        r.MarkUsed(null);
        r.MarkUsed("   ");
        Assert.Empty(r.RecentNames(10));
    }
}
