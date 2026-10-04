using System.Collections.Generic;
using Avalonia.Media;

namespace Sholto.Interface.MainUI.Theming;

public sealed class WaveformPresets(IReadOnlyDictionary<WaveformPreset, WaveformPresetColours> catalogue) : IWaveformPresets
{
    private readonly IReadOnlyDictionary<WaveformPreset, WaveformPresetColours> _catalogue = catalogue;

    /// <summary>Denon/Rekordbox 3-band: low blue, mid orange, high white.</summary>
    public (Color Low, Color Mid, Color High) ThreeBand => Bands(WaveformPreset.Bands);

    public (Color Low, Color Mid, Color High) Bands(WaveformPreset p)
    {
        var c = _catalogue.TryGetValue(p, out var found) ? found : _catalogue[WaveformPreset.Bands];
        return (c.Low, c.Mid, c.High);
    }

    /// <summary>Downbeat-guide colour chosen to contrast each preset's high band.</summary>
    public Color Downbeat(WaveformPreset p) =>
        (_catalogue.TryGetValue(p, out var found) ? found : _catalogue[WaveformPreset.Bands]).Downbeat;
}
