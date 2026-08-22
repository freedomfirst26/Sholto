using Xunit;
using Sholto.Analysis;

namespace Sholto.App.Tests;

public class WaveformBandScalingTests
{
    // A track: 90 quiet "breakdown" columns + 10 loud "drop" columns, so the
    // 97th-percentile reference per band lands at the loud level.
    private static (float[] low, float[] mid, float[] high) MakeTrack()
    {
        var low = new float[100];
        var mid = new float[100];
        var high = new float[100];
        for (int i = 0; i < 100; i++)
        {
            bool drop = i >= 90;
            low[i]  = drop ? 1.0f : 0.10f;
            mid[i]  = drop ? 0.3f : 0.10f;
            high[i] = drop ? 0.2f : 0.05f;
        }
        return (low, mid, high);
    }

    [Fact]
    public void Breakdown_ShowsLittleBlue_DropShowsFull()
    {
        var (low, mid, high) = MakeTrack();
        var s = WaveformBandScaling.Calibrate(low, mid, high);

        var bd = s.Normalize(0.10f, 0.10f, 0.05f); // breakdown column
        var dr = s.Normalize(1.00f, 0.30f, 0.20f); // drop column

        Assert.True(bd.Low < 0.2f, $"breakdown blue should be tiny, was {bd.Low}");
        Assert.True(dr.Low > 0.9f, $"drop blue should be near full, was {dr.Low}");
    }

    [Fact]
    public void Absolute_FixesTheProportionalTrap()
    {
        var (low, mid, high) = MakeTrack();
        var s = WaveformBandScaling.Calibrate(low, mid, high);

        // The old proportional render painted the breakdown 0.1/(0.1+0.1+0.05)=0.4 blue.
        float proportional = 0.10f / (0.10f + 0.10f + 0.05f);
        var bd = s.Normalize(0.10f, 0.10f, 0.05f);

        Assert.True(bd.Low < proportional * 0.6f,
            $"absolute blue {bd.Low} should sit well below the proportional {proportional}");
    }

    [Fact]
    public void SilhouetteHeight_TracksIntensity()
    {
        var (low, mid, high) = MakeTrack();
        var s = WaveformBandScaling.Calibrate(low, mid, high);

        var bd = s.Normalize(0.10f, 0.10f, 0.05f);
        var dr = s.Normalize(1.00f, 0.30f, 0.20f);
        float bdTotal = bd.Low + bd.Mid + bd.High;
        float drTotal = dr.Low + dr.Mid + dr.High;

        Assert.True(drTotal > bdTotal * 2, $"drop {drTotal} should tower over breakdown {bdTotal}");
    }

    [Fact]
    public void EmptyTrack_DoesNotThrow()
    {
        var s = WaveformBandScaling.Calibrate([], [], []);
        var n = s.Normalize(0f, 0f, 0f);
        Assert.Equal(0f, n.Low);
    }
}
