using Avalonia.Media;

namespace Sholto.Interface.MainUI.Theming;

/// <summary>The named colours a <see cref="WaveformPreset"/> contributes to a waveform palette.</summary>
public interface IWaveformPresets
{
    /// <summary>Denon/Rekordbox 3-band: low blue, mid orange, high white.</summary>
    (Color Low, Color Mid, Color High) ThreeBand { get; }

    /// <summary>Downbeat-guide colour chosen to contrast the preset's high band.</summary>
    Color Downbeat(WaveformPreset p);
}
