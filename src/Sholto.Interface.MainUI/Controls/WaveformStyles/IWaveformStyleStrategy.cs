using Sholto.Interface.MainUI.Theming;
using SkiaSharp;
using Sholto.Data;

namespace Sholto.Interface.MainUI.Controls.WaveformStyles;

/// <summary>One way of drawing a deck's waveform body (Rekordbox calls these styles: 3-BAND, RGB, …).
/// <see cref="WaveformControl"/> hands a track's peaks to the active strategy once per track, palette or
/// style change, on a background thread, and blits the returned image every frame; everything drawn live
/// on top (beatgrid, playhead, loop, vocal lane, markers) is the control's and is the same for every
/// style.</summary>
public interface IWaveformStyleStrategy
{
    /// <summary>Stable identifier, persisted as the user's choice. Never rename one: a saved id that no
    /// longer matches falls back to the default style.</summary>
    string Id { get; }

    /// <summary>The name the DJ sees (Rekordbox's: "3-BAND", "RGB").</summary>
    string DisplayName { get; }

    /// <summary>Render the whole track's waveform body into an offscreen image, one column per peak.
    /// Runs off the UI thread. Returns null when there is nothing to draw or <paramref name="ct"/> was
    /// cancelled.</summary>
    SKImage? Bake(WaveformPeaks peaks, WaveformPalette palette, CancellationToken ct);

    /// <summary>True when an image baked with <paramref name="baked"/> would look the same with
    /// <paramref name="next"/> — the palette colours this style bakes are unchanged — so a theme switch
    /// can skip the rebake.</summary>
    bool BakesSameColours(WaveformPalette baked, WaveformPalette next);
}
