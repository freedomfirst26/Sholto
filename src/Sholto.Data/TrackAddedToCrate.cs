namespace Sholto.Data;

/// <summary>A track was added to a crate (a toast-worthy fact).</summary>
public readonly record struct TrackAddedToCrate(string CrateName, Guid TrackId) : IEvent;
