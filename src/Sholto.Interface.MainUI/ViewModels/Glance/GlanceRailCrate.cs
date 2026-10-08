using Sholto.Data;

namespace Sholto.Interface.MainUI.ViewModels.Glance;

/// <summary>A crate in the rail, with its place in the chip filter.</summary>
/// <param name="Crate">The crate.</param>
/// <param name="IsActive">A chip for this crate is in the search box.</param>
/// <param name="Count">Tracks you would get by adding it: the chip scope's overlap with the crate while any chip
/// is active, otherwise the crate's own size. Follows the applied result, not the request.</param>
/// <param name="IsZero">Adding it would leave nothing. Never set on an active item.</param>
/// <param name="IsInTrackList">Its source is already in the Track List.</param>
public sealed record GlanceRailCrate(CrateRef Crate, bool IsActive, int Count, bool IsZero, bool IsInTrackList = false)
{
    public int Id => Crate.Id;

    public string Name => Crate.Name;
}
