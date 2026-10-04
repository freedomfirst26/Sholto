using Avalonia.Media;

namespace Sholto.Interface.MainUI.Theming;

/// <summary>Status colours: ok / warn / error for the status bar, attention for the analysis-failed marker. Base colours; the code applies any tint alpha.</summary>
public sealed record StatusPalette(Color Ok, Color Warn, Color Error, Color Attention);
