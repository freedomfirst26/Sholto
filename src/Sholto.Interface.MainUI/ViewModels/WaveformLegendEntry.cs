using Avalonia.Media;

namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>One swatch + label under a waveform style card ("Low", "Bass", …), coloured from the
/// current theme's waveform palette.</summary>
public sealed record WaveformLegendEntry(string Label, IBrush Swatch);
