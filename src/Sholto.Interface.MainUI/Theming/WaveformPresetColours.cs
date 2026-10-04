using Avalonia.Media;

namespace Sholto.Interface.MainUI.Theming;

/// <summary>One catalogue entry from <c>waveform-presets.json</c>: a preset's three band
/// colours and its downbeat-guide colour.</summary>
public sealed class WaveformPresetColours(Color low, Color mid, Color high, Color downbeat)
{
    public Color Low { get; } = low;
    public Color Mid { get; } = mid;
    public Color High { get; } = high;
    public Color Downbeat { get; } = downbeat;
}
