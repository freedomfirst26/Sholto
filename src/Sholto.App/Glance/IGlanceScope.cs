using Sholto.App.Library.Crates;
using Sholto.Data;

namespace Sholto.App.Glance;

/// <summary>Narrows the catalogue to the crates and tags the person has picked, before text and fit are applied.</summary>
public interface IGlanceScope
{
    /// <summary>Keep the rows in every crate of <paramref name="crateIds"/> and carrying every tag of <paramref name="tags"/>
    /// (AND; tags match by exact name, ignoring case). The "All Tracks" crate applies no constraint; an unknown crate id
    /// leaves nothing. With no crates and no tags the scope is the whole catalogue and the counts are null.</summary>
    GlanceScopeResult Apply(IReadOnlyList<TrackSummary> catalogue, CrateMembership membership,
        IReadOnlyList<int> crateIds, IReadOnlyList<string> tags);
}
