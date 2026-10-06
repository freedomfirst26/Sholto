using Avalonia.Media;

namespace Sholto.Interface.MainUI.Theming;

/// <summary>
/// Every colour <see cref="Sholto.Interface.MainUI.Controls.WaveformControl"/> draws. Comes from
/// an optional "waveform" JSON section (<see cref="SholtoThemeFactory"/>); any key —
/// or the whole section — left out is filled by <see cref="WaveformPaletteFactory"/> so no
/// theme needs editing to stay valid. Alpha is part of the colour: the defaults
/// carry the same alphas the control used to hard-code.
/// High, Vocal, VocalInactive, SnapGlow, GridEdit, Edge and RgbLow/RgbMid/RgbHigh default to the values in
/// <c>defaults.json</c> (base colours; alphas the control applies stay in code).
/// </summary>
public sealed record WaveformPalette(
    Color Background,   // baked image background
    Color Low,          // bass band
    Color Mid,
    Color Downbeat,     // bar guide line
    Color BeatTick,     // per-beat tick
    Color Playhead,
    Color Marker,       // user markers / cue pins
    Color Gain,         // channel/crossfader gain line
    Color Loop,         // active loop band (translucent)
    Color High,         // innermost band
    Color Vocal,        // vocal lane, active
    Color VocalInactive,
    Color SnapGlow,     // beat-snap glow
    Color GridEdit,     // loop/grid tint once the grid was nudged
    Color Edge,         // waveform edge outline (base colour)
    Color RgbLow,       // RGB style: bass weight colour
    Color RgbMid,       // RGB style: mid weight colour
    Color RgbHigh);     // RGB style: high weight colour
