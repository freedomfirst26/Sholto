using Microsoft.Extensions.Options;
using Sholto.App.Analysis.Stems;
using Sholto.TestSupport.Allocation;
using Xunit;

namespace Sholto.App.Audio.Tests;

/// <summary>
/// The 1x-speed stem sum in <see cref="StemMixDataProvider"/> runs on vectors with a
/// scalar tail. Each case reads one buffer at unity speed and compares every output
/// sample, bit for bit, to the old scalar sum computed inline here.
/// </summary>
public class StemMixSimdTests
{
    private const int TrackFrames = 4096;

    public static TheoryData<int, int> Lengths => new()
    {
        // frames under test, frames read first (so the read starts mid-vector in the source)
        { 1, 0 }, { 7, 0 }, { 8, 0 }, { 513, 0 }, { 1024, 0 },
        { 1, 3 }, { 7, 5 }, { 513, 3 }, { 1031, 1 },
    };

    [Theory]
    [MemberData(nameof(Lengths))]
    public void Unity_gain_sum_matches_the_scalar_sum_bit_for_bit(int frames, int skipFrames)
    {
        var stems = Stems();
        var provider = Provider(stems);

        var actual = Read(provider, frames, skipFrames);

        var expected = new float[frames * 2];
        int start = skipFrames * 2;
        for (int i = 0; i < expected.Length; i++)
        {
            int k = start + i;
            expected[i] = stems.Drums[k] + stems.Vocals[k] + stems.Bass[k] + stems.Other[k];
        }
        AssertBitEqual(expected, actual);
    }

    [Theory]
    [MemberData(nameof(Lengths))]
    public void Gained_sum_matches_the_scalar_sum_bit_for_bit(int frames, int skipFrames)
    {
        var stems = Stems();
        var provider = Provider(stems);
        float g0 = 0.3f, g1 = 1.7f, g2 = 0.0001f, g3 = 0.9f;
        provider.SetGain(StemMixDataProvider.Drums, g0);
        provider.SetGain(StemMixDataProvider.Vocals, g1);
        provider.SetGain(StemMixDataProvider.Bass, g2);
        provider.SetGain(StemMixDataProvider.Other, g3);

        var actual = Read(provider, frames, skipFrames);

        var expected = new float[frames * 2];
        int start = skipFrames * 2;
        for (int i = 0; i < expected.Length; i++)
        {
            int k = start + i;
            expected[i] = stems.Drums[k] * g0 + stems.Vocals[k] * g1
                        + stems.Bass[k] * g2 + stems.Other[k] * g3;
        }
        AssertBitEqual(expected, actual);
    }

    [Fact]
    public void Unity_speed_read_allocates_nothing()
    {
        var provider = Provider(Stems());
        provider.SetGain(StemMixDataProvider.Bass, 0.5f);
        var buffer = new float[513 * 2];
        provider.ReadBytes(buffer); // warm up (JIT)

        var result = new AllocationWindows(5).Measure(() =>
        {
            for (int i = 0; i < 3; i++) provider.ReadBytes(buffer);
        });

        Assert.True(result.AllocatingWindows <= 1, result.ToString());
    }

    private static float[] Read(StemMixDataProvider provider, int frames, int skipFrames)
    {
        // No Seek: a seek arms the declick fade, which would change the first frames.
        if (skipFrames > 0) provider.ReadBytes(new float[skipFrames * 2]);
        var buffer = new float[frames * 2];
        Assert.Equal(buffer.Length, provider.ReadBytes(buffer));
        return buffer;
    }

    private static StemMixDataProvider Provider(StemSamples stems) =>
        new(stems, 48_000, Options.Create(new LoopOptions()));

    // Values over several orders of magnitude, so the order of the adds decides the rounding.
    private static StemSamples Stems()
    {
        var rng = new Random(145);
        float[] Stem()
        {
            var a = new float[TrackFrames * 2];
            for (int i = 0; i < a.Length; i++)
                a[i] = (float)((rng.NextDouble() * 2 - 1) * Math.Pow(10, rng.Next(-4, 2)));
            return a;
        }
        return new StemSamples(Stem(), Stem(), Stem(), Stem());
    }

    private static void AssertBitEqual(float[] expected, float[] actual)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.True(BitConverter.SingleToInt32Bits(expected[i]) == BitConverter.SingleToInt32Bits(actual[i]),
                $"sample {i}: expected {expected[i]:R}, actual {actual[i]:R}");
        }
    }
}
