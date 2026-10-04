using Avalonia.Media;

namespace Sholto.Interface.MainUI.Theming;

/// <summary>The four colour stops of the deck's position ring, from start to end of the track.</summary>
public sealed record RingPalette(Color Start, Color Mid, Color Late, Color End);
