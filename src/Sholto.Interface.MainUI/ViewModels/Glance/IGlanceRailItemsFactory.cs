using Sholto.Data;

namespace Sholto.Interface.MainUI.ViewModels.Glance;

/// <summary>Makes the items the Glance rail lists.</summary>
public interface IGlanceRailItemsFactory
{
    /// <summary>The "CRATES" header with its count and the crate items, then the "TAGS" header and the tag items.
    /// A crate or tag is active when a chip filters on it; its count is the scope count when one is known, else its own.</summary>
    IReadOnlyList<object> Create(IReadOnlyList<CrateRef> crates, IReadOnlyList<TagHit> tags,
        IReadOnlyList<GlanceChip> chips, IReadOnlyDictionary<int, int>? crateCounts,
        IReadOnlyDictionary<string, int>? tagCounts, IReadOnlySet<string> trackListKeys);
}
