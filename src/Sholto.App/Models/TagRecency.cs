namespace Sholto.App.Models;

/// <summary>
/// In-memory record of when each tag was last selected, shared by every panel
/// that lists tags. "Selected" means the user committed the tag in the tag
/// editor, or picked it in the search overlay — not merely typed near it.
/// <para>Deliberately not persisted: this is a per-session convenience, so it
/// is empty again after a restart and the panels fall back to their normal
/// alphabetical / most-used order.</para>
/// </summary>
public sealed class TagRecency
{
    /// <summary>When a tag was last selected, plus the order it happened in.
    /// The sequence number breaks ties: two selections inside one clock tick
    /// would otherwise sort arbitrarily.</summary>
    private readonly record struct Use(DateTime At, long Sequence);

    private readonly object _gate = new();
    private readonly Dictionary<string, Use> _lastUsed = new(StringComparer.OrdinalIgnoreCase);
    private long _sequence;

    /// <summary>Stamps <paramref name="name"/> as used now.</summary>
    public void MarkUsed(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        lock (_gate) _lastUsed[name.Trim()] = new Use(DateTime.UtcNow, ++_sequence);
    }

    /// <summary>The time <paramref name="name"/> was last selected, or null.</summary>
    public DateTime? LastUsed(string name)
    {
        lock (_gate) return _lastUsed.TryGetValue(name, out var use) ? use.At : null;
    }

    /// <summary>Every remembered tag, most recently selected first.</summary>
    public IReadOnlyList<string> RecentNames(int limit)
    {
        lock (_gate)
            return _lastUsed.OrderByDescending(kv => kv.Value.Sequence)
                            .Take(limit)
                            .Select(kv => kv.Key)
                            .ToList();
    }

    /// <summary>
    /// Reorders <paramref name="items"/> so remembered tags come first, newest
    /// first. Items with no record keep their incoming order behind them, so an
    /// empty store leaves the caller's ordering untouched.
    /// </summary>
    public IReadOnlyList<T> OrderRecentFirst<T>(IEnumerable<T> items, Func<T, string> nameOf)
    {
        return items.Select((item, index) => (item, index))
                    .OrderByDescending(x => SequenceOf(nameOf(x.item)))
                    .ThenBy(x => x.index)
                    .Select(x => x.item)
                    .ToList();
    }

    private long SequenceOf(string name)
    {
        lock (_gate) return _lastUsed.TryGetValue(name, out var use) ? use.Sequence : 0;
    }
}
