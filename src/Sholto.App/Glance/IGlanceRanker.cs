using Sholto.Data;

namespace Sholto.App.Glance;

/// <summary>Filters rows by a Glance query and orders them by fit to the reference deck.</summary>
public interface IGlanceRanker
{
    /// <summary>Rank <paramref name="rows"/> against <paramref name="reference"/> (null = fit off) for <paramref name="query"/>.</summary>
    RankedTracks Rank(IReadOnlyList<TrackSummary> rows, GlanceReference? reference, string query);
}
