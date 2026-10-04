using Avalonia.Media;

namespace Sholto.Interface.MainUI.Theming;

/// <summary>The Faceplate amber language as base colours (alpha stays in the Faceplate code).</summary>
public sealed record FaceplatePalette(Color Rest, Color Hover, Color Selected, Color Glow);
