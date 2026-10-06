using System;
using System.IO;
using Sholto.Interface.MainUI.Theming;

namespace Sholto.Interface.MainUI.Tests;

/// <summary>Builds <see cref="WaveformPresets"/> from the real bundled catalogue file.</summary>
internal sealed class TestWaveformPresets
{
    public WaveformPresets Create()
    {
        var dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "src", "Sholto.Interface.MainUI", "Themes", "waveform-presets.json")))
            dir = Path.GetDirectoryName(dir) ?? throw new FileNotFoundException("waveform-presets.json");
        var json = File.ReadAllText(Path.Combine(dir, "src", "Sholto.Interface.MainUI", "Themes", "waveform-presets.json"));
        return new WaveformPresets(new WaveformPresetCatalogueFactory().Create(json));
    }
}
