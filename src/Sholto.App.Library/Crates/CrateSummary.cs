namespace Sholto.App.Library.Crates;

/// <summary>A crate and its current track count — for list/search UIs.</summary>
public sealed record CrateSummary(int Id, string Name, int TrackCount);
