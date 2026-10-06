namespace Sholto.Interface.MainUI.ViewModels.Glance;

/// <summary>A crate or a tag the person added to the search box. Chips stack and the list must match all of them.</summary>
/// <param name="Kind">Crate or tag.</param>
/// <param name="CrateId">The crate's id; 0 for a tag.</param>
/// <param name="Name">The crate's or the tag's name, as the rail showed it.</param>
public sealed record GlanceChip(GlanceChipKind Kind, int CrateId, string Name);
