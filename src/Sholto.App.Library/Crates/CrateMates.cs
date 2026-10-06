namespace Sholto.App.Library.Crates;

/// <summary>Every crate containing a track (except "All Tracks") and the union of those crates' members.</summary>
/// <param name="CrateNames">Names of the crates, sorted.</param>
/// <param name="TrackIds">Ids of every track in any of those crates.</param>
public sealed record CrateMates(IReadOnlyList<string> CrateNames, IReadOnlySet<Guid> TrackIds);
