namespace Sholto.Interface.MainUI.Controls.Minimap;

/// <summary>Where the per-frame overlays sit on the strip: pure arithmetic, no drawing.</summary>
public interface IMinimapGeometry
{
    /// <summary>The x of a position given as a fraction of the track (0..1, clamped).</summary>
    double X(double fraction, double width);

    /// <summary>The x of a time in the track; 0 when the track length is unknown.</summary>
    double XOfSeconds(double seconds, double trackSeconds, double width);

    /// <summary>The x of a bar: its time is <c>firstDownbeatSeconds + bar * barPeriodSeconds</c>; 0 when the
    /// track length is unknown.</summary>
    double XOfBar(int bar, double firstDownbeatSeconds, double barPeriodSeconds, double trackSeconds, double width);

    /// <summary>The span between two times, clamped to the strip; null when it is empty or the track
    /// length is unknown.</summary>
    MinimapSpan? Region(double startSeconds, double endSeconds, double trackSeconds, double width);
}
