using Avalonia.Media;

namespace Sholto.Interface.MainUI.Theming;

/// <summary>Colours of the three load destinations (LOAD TO slots and keys): deck 1, deck 2 and the Track List.
/// Theme JSON block <c>"load": { "deck1", "deck2", "trackList" }</c>, optional, falling back to defaults.json.</summary>
public sealed record LoadPalette(Color Deck1, Color Deck2, Color TrackList);
