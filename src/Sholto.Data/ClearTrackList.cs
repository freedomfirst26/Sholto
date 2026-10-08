namespace Sholto.Data;

/// <summary>Empty the Track List. It stays empty until something is loaded.</summary>
/// <param name="Origin">Who sent it.</param>
public readonly record struct ClearTrackList(Origin Origin) : ICommand;
