namespace Sholto.App.Library;

/// <summary>Free-text matching of a query against a haystack: whitespace-separated
/// tokens are AND'ed, each a case-insensitive substring match.</summary>
public interface ILibrarySearch
{
    /// <summary>Match a pre-built haystack against the query. Empty query matches everything.</summary>
    bool Matches(string query, string haystack);
}
