namespace Sholto.Data;

/// <summary>A tag and how many tracks carry it, as a query answers it.</summary>
public sealed record TagHit(string Name, int TrackCount);
