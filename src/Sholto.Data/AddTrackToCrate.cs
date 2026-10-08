namespace Sholto.Data;

/// <summary>Add a track to a crate; with <see cref="Create"/> the crate named <see cref="CrateName"/> is
/// created first (and <see cref="CrateId"/> is ignored). Reported by <see cref="TrackAddedToCrate"/>.</summary>
public readonly record struct AddTrackToCrate(Guid TrackId, int CrateId, string CrateName, bool Create, Origin Origin) : ICommand;
