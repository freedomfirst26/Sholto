namespace Sholto.Data;

/// <summary>A crate and its current track count, as a query answers it.</summary>
public sealed record CrateRef(int Id, string Name, int TrackCount);
