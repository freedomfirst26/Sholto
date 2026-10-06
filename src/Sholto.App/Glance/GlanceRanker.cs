using Sholto.Data;

namespace Sholto.App.Glance;

/// <inheritdoc cref="IGlanceRanker"/>
public sealed class GlanceRanker(IFitScorer scorer, IGlanceQueryFactory queryFactory, IGlanceMatcher matcher) : IGlanceRanker
{
    private const double MissingDelta = 99.0;

    private readonly IFitScorer _scorer = scorer;
    private readonly IGlanceQueryFactory _queryFactory = queryFactory;
    private readonly IGlanceMatcher _matcher = matcher;

    public RankedTracks Rank(IReadOnlyList<TrackSummary> rows, GlanceReference? reference, string query)
    {
        GlanceQuery parsed = _queryFactory.Create(query);
        bool fitActive = reference is { Key: not null, Bpm: not null };

        var scored = new List<(RankedTrack Row, int Score)>();
        foreach (TrackSummary row in rows)
        {
            if (!_matcher.Matches(row, parsed))
                continue;

            bool isReference = reference?.FilePath is { } path && string.Equals(path, row.FilePath, StringComparison.Ordinal);
            if (fitActive)
            {
                FitResult fit = _scorer.Score(row, reference!.Value);
                scored.Add((new RankedTrack(row, fit.Level, fit.TempoDeltaPercent, isReference), fit.Score));
            }
            else
            {
                scored.Add((new RankedTrack(row, FitLevel.None, null, isReference), -1));
            }
        }

        IEnumerable<RankedTrack> ordered = fitActive
            ? scored
                .OrderBy(s => s.Row.IsReference || s.Row.Summary.IsPlayed ? 1 : 0)
                .ThenByDescending(s => s.Row.Fit)
                .ThenByDescending(s => s.Score)
                .ThenBy(s => s.Row.TempoDeltaPercent is { } d ? Math.Abs(d) : MissingDelta)
                .Select(s => s.Row)
            : scored
                .OrderBy(s => s.Row.Summary.Artist, StringComparer.OrdinalIgnoreCase)
                .ThenBy(s => s.Row.Summary.Title, StringComparer.OrdinalIgnoreCase)
                .Select(s => s.Row);

        return new RankedTracks(
            ordered.ToList(),
            reference?.Deck ?? -1,
            reference?.Key,
            reference?.Bpm,
            fitActive,
            parsed.FilterChips);
    }
}
