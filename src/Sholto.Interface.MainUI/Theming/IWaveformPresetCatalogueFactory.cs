using System.Collections.Generic;

namespace Sholto.Interface.MainUI.Theming;

/// <summary>Creates the waveform preset catalogue (<c>waveform-presets.json</c>).</summary>
public interface IWaveformPresetCatalogueFactory
{
    IReadOnlyDictionary<WaveformPreset, WaveformPresetColours> Create(string json);
}
