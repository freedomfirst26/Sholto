namespace Sholto.Data;

/// <summary>Append a snapshot of a crate's songs to the Track List, de-duplicated, as one source named <paramref name="Name"/>.</summary>
/// <param name="Origin">Who sent it.</param>
public readonly record struct LoadCrateToTrackList(int CrateId, string Name, Origin Origin) : ICommand;
