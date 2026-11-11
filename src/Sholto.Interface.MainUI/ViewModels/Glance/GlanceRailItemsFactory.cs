using Sholto.Data;

namespace Sholto.Interface.MainUI.ViewModels.Glance;

public sealed class GlanceRailItemsFactory : IGlanceRailItemsFactory
{
    public IReadOnlyList<object> Create(IReadOnlyList<CrateRef> crates, IReadOnlyList<TagHit> tags,
        IReadOnlyList<GlanceChip> chips, IReadOnlyDictionary<int, int>? crateCounts,
        IReadOnlyDictionary<string, int>? tagCounts, IReadOnlySet<string> trackListKeys)
    {
        var items = new List<object>
        {
            new GlanceRailHeader("CRATES", crates.Count),
        };
        items.AddRange(crates.Select(c => RailCrate(c, chips, crateCounts, trackListKeys)));
        items.Add(new GlanceRailHeader("TAGS", tags.Count));
        items.AddRange(tags.Select(t => RailTag(t, chips, tagCounts, trackListKeys)));
        return items;
    }

    private GlanceRailCrate RailCrate(CrateRef crate, IReadOnlyList<GlanceChip> chips,
        IReadOnlyDictionary<int, int>? crateCounts, IReadOnlySet<string> trackListKeys)
    {
        var active = chips.Any(c => c.Kind == GlanceChipKind.Crate && c.CrateId == crate.Id);
        var count = crateCounts is { } counts ? counts.GetValueOrDefault(crate.Id) : crate.TrackCount;
        return new GlanceRailCrate(crate, active, count, count == 0 && !active, trackListKeys.Contains($"crate:{crate.Id}"));
    }

    private GlanceRailTag RailTag(TagHit tag, IReadOnlyList<GlanceChip> chips,
        IReadOnlyDictionary<string, int>? tagCounts, IReadOnlySet<string> trackListKeys)
    {
        var active = chips.Any(c => c.Kind == GlanceChipKind.Tag
                                    && string.Equals(c.Name, tag.Name, StringComparison.OrdinalIgnoreCase));
        var count = tagCounts is { } counts ? counts.GetValueOrDefault(tag.Name) : tag.TrackCount;
        return new GlanceRailTag(tag, active, count, count == 0 && !active,
            trackListKeys.Contains("tag:" + tag.Name.ToLowerInvariant()));
    }
}
