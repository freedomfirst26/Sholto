using Sholto.Library;

namespace Sholto.Library.Tests;

public class LibrarySearchTests
{
    private static Track T(string artist, string title) =>
        new(FilePath: $"/tmp/{artist}-{title}.mp3", Title: title, Artist: artist, Duration: TimeSpan.FromMinutes(3));

    private static readonly Track[] Catalogue =
    [
        T("Seba",           "Identity"),
        T("Seba",           "Storm"),
        T("Silence Groove", "Dustbowl"),
        T("Silence Groove", "Element Late"),
        T("Sub Focus",      "Original Don"),
        T("Skrillex",       "Bangarang"),
    ];

    [Fact]
    public void Filter_EmptyQuery_ReturnsAll()
    {
        var r = LibrarySearch.Filter("", Catalogue).ToList();
        Assert.Equal(Catalogue.Length, r.Count);
    }

    [Fact]
    public void Filter_WhitespaceQuery_ReturnsAll()
    {
        var r = LibrarySearch.Filter("   ", Catalogue).ToList();
        Assert.Equal(Catalogue.Length, r.Count);
    }

    [Fact]
    public void Filter_MatchesTitleSubstring_CaseInsensitive()
    {
        var r = LibrarySearch.Filter("storm", Catalogue).ToList();
        Assert.Single(r);
        Assert.Equal("Storm", r[0].Title);
    }

    [Fact]
    public void Filter_MatchesArtistSubstring_CaseInsensitive()
    {
        var r = LibrarySearch.Filter("SEBA", Catalogue).ToList();
        Assert.Equal(2, r.Count);
        Assert.All(r, t => Assert.Equal("Seba", t.Artist));
    }

    [Fact]
    public void Filter_MatchesAcrossArtistAndTitle()
    {
        // "groove" matches the artist "Silence Groove"
        var r = LibrarySearch.Filter("groove", Catalogue).ToList();
        Assert.Equal(2, r.Count);
    }

    [Fact]
    public void Filter_NoMatches_ReturnsEmpty()
    {
        var r = LibrarySearch.Filter("zzzzzzz", Catalogue).ToList();
        Assert.Empty(r);
    }

    [Fact]
    public void Filter_TokenisedQuery_AllTokensMustMatch()
    {
        // "silence late" matches "Silence Groove - Element Late" only (artist
        // has "silence", title has "late"). The other Silence Groove tracks
        // don't have "late" anywhere, so they're excluded.
        var r = LibrarySearch.Filter("silence late", Catalogue).ToList();
        Assert.Single(r);
        Assert.Equal("Element Late", r[0].Title);
    }
}
