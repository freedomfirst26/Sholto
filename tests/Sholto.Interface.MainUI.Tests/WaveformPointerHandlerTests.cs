using Sholto.Data;
using Sholto.Interface.MainUI.Controls;

namespace Sholto.Interface.MainUI.Tests;

public class WaveformPointerHandlerTests
{
    private readonly IWaveformPointerHandler _handler = new WaveformPointerHandler();

    private static WaveformPeaks Peaks(int n, int samplesPerPeak) =>
        new(new float[n], new float[n], new float[n], new float[n], new float[n], samplesPerPeak, 48000);

    private static readonly WaveformPeaks Body = Peaks(1000, 480);   // 0.01 s per column
    private static readonly WaveformPeaks Grid = Peaks(500, 960);    // 0.02 s per column

    [Fact]
    public void CentreClick_IsThePlayPosition()
    {
        Assert.True(_handler.TryAnchorSeconds(100, 200, 0.5, 1, null, Body, out var s));
        Assert.Equal(5.0, s, 9);
    }

    [Fact]
    public void Speed_ScalesTheOffset()
    {
        Assert.True(_handler.TryAnchorSeconds(150, 200, 0.5, 2, null, Body, out var s));
        Assert.Equal(6.0, s, 9);
    }

    [Fact]
    public void NearZeroSpeed_TreatedAsUnity()
    {
        Assert.True(_handler.TryAnchorSeconds(150, 200, 0.5, 0, null, Body, out var s));
        Assert.Equal(5.5, s, 9);
    }

    [Fact]
    public void ClickBeforeStart_ClampsToZero()
    {
        Assert.True(_handler.TryAnchorSeconds(0, 200, 0, 1, null, Body, out var s));
        Assert.Equal(0.0, s, 9);
    }

    [Fact]
    public void GridPeaks_WinOverBody()
    {
        Assert.True(_handler.TryAnchorSeconds(150, 200, 0.5, 1, Grid, Body, out var s));
        Assert.Equal(6.0, s, 9);
    }

    [Fact]
    public void EmptyGridPeaks_FallsBackToBody()
    {
        Assert.True(_handler.TryAnchorSeconds(150, 200, 0.5, 1, Peaks(0, 960), Body, out var s));
        Assert.Equal(5.5, s, 9);
    }

    [Fact]
    public void NoPeaks_ReturnsFalse()
    {
        Assert.False(_handler.TryAnchorSeconds(100, 200, 0.5, 1, null, null, out _));
        Assert.False(_handler.TryAnchorSeconds(100, 200, 0.5, 1, Peaks(0, 960), Peaks(0, 480), out _));
    }
}
