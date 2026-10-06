using Sholto.App.Library.Crates;
using Sholto.Data;

namespace Sholto.App.Glance;

/// <inheritdoc cref="IGlanceScope"/>
public sealed class GlanceScope : IGlanceScope
{
    public GlanceScopeResult Apply(IReadOnlyList<TrackSummary> catalogue, CrateMembership membership,
        IReadOnlyList<int> crateIds, IReadOnlyList<string> tags)
    {
        if (crateIds.Count == 0 && tags.Count == 0) return new GlanceScopeResult(catalogue, null, null);

        var required = new List<IReadOnlySet<Guid>>();
        var impossible = false;
        foreach (var id in crateIds)
        {
            if (id == membership.AllTracksId) continue;
            if (membership.TracksByCrate.TryGetValue(id, out var members)) required.Add(members);
            else impossible = true;
        }

        var rows = new List<TrackSummary>();
        if (!impossible)
        {
            foreach (var row in catalogue)
            {
                if (required.All(set => set.Contains(row.TrackId)) && tags.All(tag => HasTag(row, tag)))
                    rows.Add(row);
            }
        }

        var crateCounts = membership.TracksByCrate.ToDictionary(c => c.Key, c => rows.Count(r => c.Value.Contains(r.TrackId)));
        var tagCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            foreach (var tag in row.Tags.Distinct(StringComparer.OrdinalIgnoreCase))
                tagCounts[tag] = tagCounts.GetValueOrDefault(tag) + 1;
        }
        return new GlanceScopeResult(rows, crateCounts, tagCounts);
    }

    private bool HasTag(TrackSummary row, string tag) => row.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase);
}
