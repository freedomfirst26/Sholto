using System.Collections.Generic;

namespace Sholto.Interface.MainUI.Theming;

/// <summary>Parses the waveform preset catalogue (<c>waveform-presets.json</c>).</summary>
public interface IWaveformPresetsJson
{
    IReadOnlyDictionary<WaveformPreset, WaveformPresetColours> Parse(string json);
}
