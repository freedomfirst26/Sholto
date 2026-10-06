using Sholto.Interface.MainUI.Controls;

namespace Sholto.Interface.MainUI.Tests;

public class WaveformScrollSnapTests
{
    private readonly WaveformScrollSnap _snap = new();

    [Theory]
    [InlineData(1.0, 1.0, 0.0)]
    [InlineData(1.25, 1.0, 0.0)]
    [InlineData(2.0, 1.0, 0.0)]
    [InlineData(1.25, 1.08, 37.5)]
    [InlineData(1.0, 0.92, 1920.0)]
    public void The_window_left_edge_lands_on_a_whole_device_pixel_within_a_pixel_of_the_input(
        double deviceScale, double speed, double originDeviceX)
    {
        const int totalPeaks = 20000, dstW = 1234;
        double devicePerColumn = deviceScale / speed;
        for (int k = 0; k < 40; k++)
        {
            double position = 0.37 + k * 0.78 / totalPeaks;
            double left = position * totalPeaks - dstW * speed / 2.0;

            double snapped = _snap.SnapPosition(position, totalPeaks, dstW, speed, deviceScale, originDeviceX);
            double snappedLeft = snapped * totalPeaks - dstW * speed / 2.0;

            double deviceX = originDeviceX - snappedLeft * devicePerColumn;
            Assert.InRange(Math.Abs(deviceX - Math.Round(deviceX)), 0, 1e-4);
            Assert.InRange(Math.Abs(snappedLeft - left) * devicePerColumn, 0, 0.5 + 1e-9);
        }
    }

    [Fact]
    public void Nothing_to_snap_against_returns_the_input()
    {
        Assert.Equal(0.5, _snap.SnapPosition(0.5, 0, 800, 1, 1, 0));
        Assert.Equal(0.5, _snap.SnapPosition(0.5, 100, 800, 0, 1, 0));
        Assert.Equal(0.5, _snap.SnapPosition(0.5, 100, 800, 1, 0, 0));
    }
}
