namespace Sholto.Data;

/// <summary>The Glance highlight changed. While <paramref name="Active"/>, load, re-analyse and the browse knob
/// act on the search instead of the library selection.</summary>
/// <param name="Active">True while the search overlay is open.</param>
/// <param name="FilePath">The track that load means; null means nothing (rail focused, or no results).</param>
/// <param name="Origin">Who sent it.</param>
public readonly record struct SetSearchPick(bool Active, string? FilePath, Origin Origin) : ICommand;
