using System.Text.RegularExpressions;

namespace Sholto.Storage;

/// <summary>Normalise a user-typed tag name into its canonical form.
/// Trim leading/trailing whitespace, collapse internal whitespace runs
/// to a single space. Returns null if the result is empty or longer
/// than <see cref="MaxLength"/>. Casing is preserved.</summary>
public static class TagNameNormalizer
{
    public const int MaxLength = 100;

    private static readonly Regex WhitespaceRun = new(@"\s+", RegexOptions.Compiled);

    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var collapsed = WhitespaceRun.Replace(raw.Trim(), " ");
        if (collapsed.Length == 0) return null;
        if (collapsed.Length > MaxLength) return null;
        return collapsed;
    }
}
