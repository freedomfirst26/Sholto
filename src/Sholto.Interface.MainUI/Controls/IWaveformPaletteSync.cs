using Avalonia.Media;
using Sholto.Interface.MainUI.Controls.WaveformStyles;
using Sholto.Interface.MainUI.Theming;
using SkiaSharp;

namespace Sholto.Interface.MainUI.Controls;

/// <summary>
/// Owns two things for the waveform control: the memory of which palette the baked image was
/// drawn with (and so whether a palette change needs a rebake), and the stateless colour
/// derivation from a palette (SK conversion, loop colour, live colours, grid-edit pen).
/// </summary>
internal interface IWaveformPaletteSync
{
    /// <summary>Remember the palette the current baked image was drawn with.</summary>
    void MarkBaked(WaveformPalette palette);
    /// <summary>True when nothing is baked yet, no style is set, or the style bakes different colours from the baked palette.</summary>
    bool NeedsRebake(WaveformPalette next, IWaveformStyleStrategy? style);
    SKColor ToSk(Color colour);
    /// <summary>GridEdit at alpha 0xA0 when the grid was nudged, else Loop as-is.</summary>
    SKColor LoopColour(WaveformPalette palette, bool gridNudged);
    /// <summary>Playhead, BeatTick, Marker as-is; Vocal 0xD0, VocalInactive 0xB0, SnapGlow 0xF0, Edge 0x99.</summary>
    WaveformLiveColours LiveColours(WaveformPalette palette);
    /// <summary>2 px pen in GridEdit at alpha 0xE0; the same instance while the palette instance is the same.</summary>
    IPen GridEditPen(WaveformPalette palette);
}
