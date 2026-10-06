using Sholto.Data;

namespace Sholto.App.Glance;

/// <summary>
/// Decides whether a library row satisfies a parsed Glance query. Words match as plain text or as initials; both ignore
/// case and accents, split on whitespace only, and drop every character outside a-z and 0-9.
/// </summary>
public interface IGlanceMatcher
{
    /// <summary>True when <paramref name="row"/> passes every word and filter of <paramref name="query"/>.</summary>
    bool Matches(TrackSummary row, GlanceQuery query);
}
