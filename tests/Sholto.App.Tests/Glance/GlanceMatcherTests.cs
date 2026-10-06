using Sholto.App.Glance;
using Sholto.Data;

namespace Sholto.App.Tests.Glance;

public class GlanceMatcherTests
{
    private readonly GlanceTrackBuilder _b = new();
    private readonly GlanceQueryFactory _factory = new();
    private readonly GlanceMatcher _m = new();

    private bool Hit(TrackSummary t, string q) => _m.Matches(t, _factory.Create(q));

    [Fact]
    public void Initials_find_born_slippy_nuxx()
    {
        TrackSummary t = _b.Track("Born Slippy .NUXX", "8A", 128, artist: "Underworld");
        Assert.True(Hit(t, "bsn"));
    }

    [Fact]
    public void Initials_need_two_characters_and_a_single_letter_only_substring_matches()
    {
        TrackSummary t = _b.Track("Born Slippy .NUXX", "8A", 128, artist: "Underworld");
        Assert.True(Hit(t, "b"));      // substring of "born"
        Assert.False(Hit(t, "z"));     // neither a substring nor (one character) an initials match
        Assert.False(Hit(t, "bnx"));   // not a prefix or substring of "ubsn"
    }

    [Fact]
    public void Words_are_anded_across_artist_and_title()
    {
        TrackSummary t = _b.Track("Blowback", "8A", 128, artist: "Robert Hood");
        Assert.True(Hit(t, "rob blo"));
        Assert.False(Hit(t, "rob zzz"));
    }

    [Fact]
    public void Tag_filter_matches_substring_of_a_tag()
    {
        TrackSummary t = _b.Track("T", "8A", 128, tags: "uk garage");
        Assert.True(Hit(t, "#gar"));
        Assert.False(Hit(t, "#techno"));
    }

    [Fact]
    public void Single_bpm_filter_uses_half_unit_window()
    {
        Assert.True(Hit(_b.Track("a", "8A", 127.6), "bpm:128"));
        Assert.True(Hit(_b.Track("b", "8A", 128.4), "bpm:128"));
        Assert.False(Hit(_b.Track("c", "8A", 128.6), "bpm:128"));
    }

    [Fact]
    public void Bpm_range_keeps_both_ends()
    {
        Assert.True(Hit(_b.Track("a", "8A", 124.0), "bpm:124-128"));
        Assert.True(Hit(_b.Track("b", "8A", 128.0), "bpm:124-128"));
        Assert.False(Hit(_b.Track("c", "8A", 128.1), "bpm:124-128"));
    }

    [Fact] public void Bpm_filter_compares_the_displayed_bpm() => Assert.True(Hit(_b.Track("a", "8A", 64, multiplier: 2.0), "bpm:128"));
    [Fact] public void Bpm_filter_drops_rows_without_bpm() => Assert.False(Hit(_b.Track("a", "8A", null), "bpm:128"));

    [Fact]
    public void Key_filter_keeps_that_key_only()
    {
        Assert.True(Hit(_b.Track("a", "8A", 128), "key:8a"));
        Assert.False(Hit(_b.Track("b", "8B", 128), "key:8a"));
        Assert.False(Hit(_b.Track("c", "9A", 128), "key:8a"));
        Assert.False(Hit(_b.Track("d", null, 128), "key:8a"));
    }

    [Fact] public void Empty_query_matches_everything() => Assert.True(Hit(_b.Track("a", null, null), ""));

    private TrackSummary Named(string artist, string title) => _b.Track(title, "8A", 128, artist: artist);

    [Fact] public void Apostrophe_does_not_split_a_word() { TrackSummary t = Named("Queen", "Don't Stop Me Now"); Assert.True(Hit(t, "dsmn")); Assert.False(Hit(t, "dtsmn")); }
    [Fact] public void Curly_apostrophe_does_not_split_a_word() { TrackSummary t = Named("Zed", "Don\u2019t"); Assert.True(Hit(t, "zd")); Assert.False(Hit(t, "zdt")); }
    [Fact] public void Accented_letter_does_not_split_a_word() { TrackSummary t = Named("R\u00f6yksopp", "X"); Assert.True(Hit(t, "rx")); Assert.False(Hit(t, "ryx")); }
    [Fact] public void Slash_inside_a_word_does_not_split_it() { TrackSummary t = Named("AC/DC", "Thunderstruck"); Assert.True(Hit(t, "at")); Assert.False(Hit(t, "acdt")); }
    [Fact] public void Hyphen_inside_a_word_does_not_split_it() { TrackSummary t = Named("Hip-Hop", "Zed"); Assert.True(Hit(t, "hz")); Assert.False(Hit(t, "hhz")); }
    [Fact] public void Hyphenated_query_matches_hyphenated_name() => Assert.True(Hit(Named("X", "Hip-Hop"), "hip-hop"));

    [Fact]
    public void Fred_again_gives_fa_and_dpmoot()
    {
        TrackSummary t = Named("Fred again..", "Delilah (pull me out of this)");
        Assert.True(Hit(t, "fadpmoot"));
        Assert.True(Hit(t, "dpmoot"));
        Assert.False(Hit(t, "fdpmoot"));
        Assert.False(Hit(t, "dpmoo0"));
    }

    [Fact]
    public void Artemas_initials_still_match()
    {
        TrackSummary t = Named("Artemas", "I Like The Way You Kiss Me");
        Assert.True(Hit(t, "iltwykm"));
        Assert.True(Hit(t, "iltw"));
        Assert.True(Hit(t, "ailtwykm"));
    }

    [Theory]
    [InlineData("beyonce")]
    [InlineData("Beyonc\u00e9")]
    [InlineData("beyonc\u00e9")]
    public void Plain_text_folds_accents(string q) => Assert.True(Hit(Named("Beyonc\u00e9", "Halo"), q));

    [Theory]
    [InlineData("\u00f8", "o")]
    [InlineData("\u00e6", "ae")]
    [InlineData("\u0153", "oe")]
    [InlineData("\u00df", "ss")]
    [InlineData("\u0142", "l")]
    [InlineData("\u0111", "d")]
    [InlineData("\u00f1", "n")]
    public void Undecomposed_letters_are_mapped(string letter, string folded) => Assert.True(Hit(Named("b" + letter + "b", "Z"), "b" + folded + "b"));
}
