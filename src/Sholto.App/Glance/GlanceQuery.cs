namespace Sholto.App.Glance;

/// <summary>A parsed Glance query: free words plus the <c>bpm:</c>, <c>key:</c> and <c>#tag</c> filters.</summary>
/// <param name="Words">Lower-case free words; every one must match.</param>
/// <param name="BpmMin">Lower bound of the displayed-BPM filter, or null.</param>
/// <param name="BpmMax">Upper bound of the displayed-BPM filter, or null.</param>
/// <param name="Key">Upper-case Camelot code to match exactly (e.g. "8A"), or null.</param>
/// <param name="Tags">Lower-case tag fragments; every one must be contained in some tag.</param>
/// <param name="FilterChips">Display text of the filters, e.g. "BPM 124–128", "KEY 8A", "#techno".</param>
public sealed record GlanceQuery(
    IReadOnlyList<string> Words,
    double? BpmMin,
    double? BpmMax,
    string? Key,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> FilterChips);
