using Sholto.Data;

namespace Sholto.Interface.MainUI.ViewModels.Glance;

/// <summary>A tag in the rail, with its place in the chip filter.</summary>
/// <param name="Tag">The tag.</param>
/// <param name="IsActive">A chip for this tag is in the search box.</param>
/// <param name="Count">Tracks you would get by adding it: the chip scope's overlap with the tag while any chip
/// is active, otherwise the tag's own size. Follows the applied result, not the request.</param>
/// <param name="IsZero">Adding it would leave nothing. Never set on an active item.</param>
public sealed record GlanceRailTag(TagHit Tag, bool IsActive, int Count, bool IsZero)
{
    public string Name => Tag.Name;
}
