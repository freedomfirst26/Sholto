namespace Sholto.Interface.MainUI.Controls;

/// <summary>Snaps the deck's scroll so the baked waveform lands on whole device pixels. The playhead
/// advances 46.875 columns a second (1024 samples at 48 kHz per column), about 0.78 px per 60 Hz frame,
/// so the source window's left edge otherwise sits at a different sub-pixel phase every frame and the
/// bilinear blit swings per-column detail between sharp and smeared — the waveform flickers as it goes
/// past. Snapping the window's left edge to a whole DEVICE pixel (not a logical one: on a 1.25× display a
/// logical pixel is 1.25 device pixels) holds the phase, at the cost of the scroll stepping 1,1,1,0 px
/// instead of 0.78 every frame. Held by the control like <see cref="WaveformPaints"/>; pure
/// arithmetic, nothing allocated per frame.</summary>
public sealed class WaveformScrollSnap
{
    /// <summary>The play position (0..1) to draw this frame instead of <paramref name="playPosition"/>,
    /// chosen so the first visible source column's left edge maps to a whole device pixel. Every
    /// overlay must use the same returned position so the grid, loop, vocal lane and markers stay locked
    /// to the body. Returns the input unchanged when there is nothing to snap against.</summary>
    /// <param name="totalPeaks">Columns in the baked image (one per peak).</param>
    /// <param name="dstW">Control width in logical pixels.</param>
    /// <param name="playbackSpeed">Source columns per logical pixel (1 at unity tempo).</param>
    /// <param name="deviceScale">Device pixels per logical pixel (the canvas' TotalMatrix.ScaleX).</param>
    /// <param name="originDeviceX">Device x of the control's left edge (TotalMatrix.TransX).</param>
    public double SnapPosition(double playPosition, int totalPeaks, int dstW, double playbackSpeed,
        double deviceScale, double originDeviceX)
    {
        if (totalPeaks <= 0 || playbackSpeed <= 0 || deviceScale <= 0) return playPosition;
        double halfWindow = dstW * playbackSpeed / 2.0;
        double left = playPosition * totalPeaks - halfWindow;
        double snappedLeft = SnapLeft(left, deviceScale / playbackSpeed, originDeviceX);
        return (snappedLeft + halfWindow) / totalPeaks;
    }

    /// <summary>The source column (fractional) to put at the control's left edge so that column
    /// boundaries fall on whole device pixels: the nearest value to <paramref name="leftColumn"/> for which
    /// <c>originDeviceX − left · devicePerColumn</c> is an integer.</summary>
    public double SnapLeft(double leftColumn, double devicePerColumn, double originDeviceX) =>
        (originDeviceX - Math.Round(originDeviceX - leftColumn * devicePerColumn)) / devicePerColumn;
}
