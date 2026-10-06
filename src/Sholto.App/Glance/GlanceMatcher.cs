using System.Globalization;
using System.Text;
using Sholto.App.Analysis.Harmony;
using Sholto.Data;

namespace Sholto.App.Glance;

/// <inheritdoc cref="IGlanceMatcher"/>
public sealed class GlanceMatcher : IGlanceMatcher
{
    public bool Matches(TrackSummary row, GlanceQuery query)
    {
        if (query.BpmMin is { } min && query.BpmMax is { } max)
        {
            if (row.Bpm is not { } raw)
                return false;
            double shown = raw * row.BpmMultiplier;
            if (shown < min || shown > max)
                return false;
        }

        if (query.Key is { } key)
        {
            if (row.MusicalKey is not { } k || !string.Equals(new Key(k.PitchClass, k.IsMajor).ToCamelot(), key, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        foreach (string tag in query.Tags)
        {
            bool any = false;
            foreach (string t in row.Tags)
            {
                if (t.Contains(tag, StringComparison.OrdinalIgnoreCase)) { any = true; break; }
            }
            if (!any)
                return false;
        }

        if (query.Words.Count == 0)
            return true;

        string hay = Normalise(row.Artist + " " + row.Title + " " + string.Join(' ', row.Tags), initialsOnly: false);
        string titleInitials = Normalise(row.Title, initialsOnly: true);
        string artistInitials = Normalise(row.Artist, initialsOnly: true);
        foreach (string raw in query.Words)
        {
            string word = Normalise(raw, initialsOnly: false);
            if (word.Length == 0 || hay.Contains(word, StringComparison.Ordinal))
                continue;
            bool initialsHit = word.Length >= 2
                && (titleInitials.StartsWith(word, StringComparison.Ordinal)
                    || artistInitials.StartsWith(word, StringComparison.Ordinal)
                    || (artistInitials + titleInitials).Contains(word, StringComparison.Ordinal));
            if (!initialsHit)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Lowercases and folds accents, splits on whitespace, and drops every character outside a-z and 0-9 from each word
    /// (words left empty are skipped). Returns the words joined by single spaces, or only their first characters.
    /// </summary>
    private string Normalise(string text, bool initialsOnly)
    {
        bool ascii = true;
        foreach (char ch in text)
        {
            if (ch > 127) { ascii = false; break; }
        }
        if (!ascii)
            text = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);

        // A folded character may become two (æ, œ, ß); one separator per word never exceeds the input length.
        int max = text.Length * 2;
        char[]? rented = null;
        Span<char> buf = max <= 512 ? stackalloc char[512] : (rented = System.Buffers.ArrayPool<char>.Shared.Rent(max));
        try
        {
            int n = 0;
            bool inWord = false;
            bool pendingSpace = false;
            foreach (char raw in text)
            {
                if (char.IsWhiteSpace(raw))
                {
                    if (inWord) pendingSpace = true;
                    inWord = false;
                    continue;
                }
                char c = ascii ? char.ToLowerInvariant(raw) : raw;
                string? folded = Fold(c);
                int count = folded?.Length ?? 1;
                for (int i = 0; i < count; i++)
                {
                    char f = folded is null ? c : folded[i];
                    if (!(f is (>= 'a' and <= 'z') or (>= '0' and <= '9')))
                        continue;
                    if (initialsOnly)
                    {
                        if (!inWord) buf[n++] = f;
                    }
                    else
                    {
                        if (!inWord && pendingSpace && n > 0) buf[n++] = ' ';
                        buf[n++] = f;
                    }
                    inWord = true;
                    pendingSpace = false;
                }
            }
            return new string(buf[..n]);
        }
        finally
        {
            if (rented is not null)
                System.Buffers.ArrayPool<char>.Shared.Return(rented);
        }
    }

    /// <summary>The replacement for a letter NFD does not decompose, an empty string for a combining mark, or null to keep it.</summary>
    private string? Fold(char c) => c switch
    {
        'ø' => "o",
        'æ' => "ae",
        'œ' => "oe",
        'ß' => "ss",
        'ł' => "l",
        'đ' => "d",
        > (char)127 when CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark => "",
        _ => null,
    };
}
