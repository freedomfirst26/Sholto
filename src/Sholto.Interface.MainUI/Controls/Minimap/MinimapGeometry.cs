namespace Sholto.Interface.MainUI.Controls.Minimap;

public sealed class MinimapGeometry : IMinimapGeometry
{
    public double X(double fraction, double width) => Math.Clamp(fraction, 0, 1) * width;

    public double XOfSeconds(double seconds, double trackSeconds, double width) =>
        trackSeconds <= 0 ? 0 : X(seconds / trackSeconds, width);

    public double XOfBar(int bar, double firstDownbeatSeconds, double barPeriodSeconds, double trackSeconds, double width) =>
        XOfSeconds(firstDownbeatSeconds + bar * barPeriodSeconds, trackSeconds, width);

    public MinimapSpan? Region(double startSeconds, double endSeconds, double trackSeconds, double width)
    {
        if (trackSeconds <= 0 || endSeconds <= startSeconds) return null;
        return new MinimapSpan(XOfSeconds(startSeconds, trackSeconds, width), XOfSeconds(endSeconds, trackSeconds, width));
    }
}
