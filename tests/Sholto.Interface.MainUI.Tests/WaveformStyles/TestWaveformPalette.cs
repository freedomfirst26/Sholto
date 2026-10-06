using Avalonia.Media;
using Sholto.Interface.MainUI.Theming;

namespace Sholto.Interface.MainUI.Tests.WaveformStyles;

/// <summary>The palette the bake tests draw with: the real derivation over the bundled
/// waveform-presets.json and defaults.json, from fixed core colours.</summary>
internal sealed class TestWaveformPalette
{
    public WaveformPalette Create() =>
        new WaveformPaletteFactory(new TestWaveformPresets().Create(), new TestThemeDefaults().Create())
            .FromTheme(WaveformPreset.Bands, Colors.Black, Colors.Red, Colors.Green, Colors.White, Colors.Gray);
}
