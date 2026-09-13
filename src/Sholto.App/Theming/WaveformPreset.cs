namespace Sholto.App.Theming;

/// <summary>Named colour presets a theme can reference by <c>"waveformPalette"</c>.
/// Historically each preset carried its own three band colours, but the app
/// has drawn the Rekordbox 3-band scheme for every theme for a while now
/// (it reads far better for spotting the kick). So a preset only contributes
/// its downbeat-guide colour by default; its band colours are still here for a
/// theme that wants them via explicit <c>low</c>/<c>mid</c>/<c>high</c> keys.</summary>
public enum WaveformPreset { Bands, Hot, Plasma, Smoke, Glacier, OctoberRust, Massacre, Soule, Pantera }
