namespace Sholto.Data;

/// <summary>The theme the user chose last time, read from settings once the library database is open. A
/// fact: not replayed. An interface that owns themes applies it if it still has a theme of that name.</summary>
public readonly record struct SavedThemeFound(string Name) : IEvent;
