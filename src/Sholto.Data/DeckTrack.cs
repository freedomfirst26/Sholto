namespace Sholto.Data;

/// <summary>The loaded track as a deck shows it: where it lives and what to call it. Carried by
/// <see cref="DeckContentChanged"/>.</summary>
public sealed record DeckTrack(string FilePath, string Title, string Artist, TimeSpan Duration);
