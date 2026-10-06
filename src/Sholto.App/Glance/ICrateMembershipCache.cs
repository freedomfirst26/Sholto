using Sholto.App.Library.Crates;

namespace Sholto.App.Glance;

/// <summary>The crate membership snapshot Glance scopes with: fetched once, off the app thread, and refetched only
/// after it is invalidated. App thread only.</summary>
public interface ICrateMembershipCache
{
    /// <summary>The current snapshot; empty when there is no crate service yet. Starts the fetch if there is none.</summary>
    Task<CrateMembership> CurrentAsync();

    /// <summary>Crate membership changed: the next <see cref="CurrentAsync"/> refetches. A fetch already in flight is
    /// not kept as current.</summary>
    void Invalidate();
}
