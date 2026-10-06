namespace Sholto.App.Library.Crates;

/// <summary>The member track ids of every crate except "All Tracks", from one query.</summary>
/// <param name="TracksByCrate">Crate id to its member ids. An empty crate is present with an empty set.</param>
/// <param name="AllTracksId">Id of the "All Tracks" crate, or null when it does not exist.</param>
public sealed record CrateMembership(IReadOnlyDictionary<int, IReadOnlySet<Guid>> TracksByCrate, int? AllTracksId);
