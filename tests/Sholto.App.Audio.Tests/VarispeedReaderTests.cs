using Microsoft.Extensions.Options;
using Sholto.App.Analysis.Stems;
using SoundFlow.Interfaces;

namespace Sholto.App.Audio.Tests;

/// <summary>The varispeed read loop of both in-memory providers: top-speed clamp, the
/// per-buffer speed ramp, the box filter beyond 1x, and the untouched reads at steady
/// speeds up to 1x. Every test runs against the whole-track provider and the stem mix
/// (with the track in the drums stem and the other stems silent).</summary>
public class VarispeedReaderTests
{
    private const int SampleRate = 48000;

    public static TheoryData<string> Providers => new() { "scratch", "stems" };

    private static ISoundDataProvider Make(string kind, float[] interleaved, double maxSpeed = 8)
    {
        var varispeed = Options.Create(new VarispeedOptions { MaxSpeed = maxSpeed });
        if (kind == "scratch")
            return new ScratchDataProvider(interleaved, SampleRate, varispeed);
        var silent = new float[interleaved.Length];
        return new StemMixDataProvider(new StemSamples(interleaved, silent, silent, silent), SampleRate,
            Options.Create(new LoopOptions()), varispeed);
    }

    private static void SetSpeed(ISoundDataProvider provider, float speed) =>
        ((IVarispeedProvider)provider).SetSpeed(speed);

    private static float[] Read(ISoundDataProvider provider, int frames)
    {
        var buffer = new float[frames * 2];
        Assert.Equal(frames * 2, provider.ReadBytes(buffer));
        return buffer;
    }

    private static float[] Stereo(int frames, Func<int, float> sample)
    {
        var data = new float[frames * 2];
        for (int f = 0; f < frames; f++)
        {
            data[f * 2] = sample(f);
            data[f * 2 + 1] = sample(f);
        }
        return data;
    }

    // — J2: top-speed clamp —

    [Theory]
    [MemberData(nameof(Providers))]
    public void A_forward_fling_of_40x_reads_at_8x(string kind)
    {
        var provider = Make(kind, new float[200_000 * 2]);
        SetSpeed(provider, 40f);
        Read(provider, 256);
        Assert.Equal(256 * 8 * 2, provider.Position);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public void A_backspin_of_minus_40x_reads_at_minus_8x(string kind)
    {
        var provider = Make(kind, new float[200_000 * 2]);
        provider.Seek(100_000 * 2);
        SetSpeed(provider, -40f);
        Read(provider, 256);
        Assert.Equal((100_000 - 256 * 8) * 2, provider.Position);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public void The_top_speed_comes_from_the_options(string kind)
    {
        var provider = Make(kind, new float[200_000 * 2], maxSpeed: 4);
        SetSpeed(provider, 40f);
        Read(provider, 256);
        Assert.Equal(256 * 4 * 2, provider.Position);
    }

    // — J3: speed ramp —

    [Theory]
    [MemberData(nameof(Providers))]
    public void A_step_from_1x_to_8x_ramps_across_the_buffer_with_no_jump(string kind)
    {
        // Each source frame holds its own index, so every output sample is the
        // read position it came from (the centred box filter of a straight line
        // is the line itself).
        const int frames = 256;
        var provider = Make(kind, Stereo(20_000, f => f));
        var before = Read(provider, frames);
        SetSpeed(provider, 8f);
        var after = Read(provider, frames);

        var read = new float[frames * 2];
        for (int i = 0; i < frames; i++)
        {
            read[i] = before[i * 2];
            read[frames + i] = after[i * 2];
        }

        float largestChange = 0f;
        for (int i = 2; i < read.Length; i++)
        {
            float step = read[i] - read[i - 1];
            float previous = read[i - 1] - read[i - 2];
            largestChange = Math.Max(largestChange, Math.Abs(step - previous));
        }
        // The ramp adds 7/256 ≈ 0.027 frames of speed per output frame; an
        // unramped switch would jump the step by 7 at the buffer edge.
        Assert.True(largestChange < 0.05f, $"step changed by {largestChange} between adjacent frames");
        Assert.Equal(1f, read[frames - 1] - read[frames - 2], 3);
        Assert.Equal(8f, read[^1] - read[^2], 1);

        // The next buffer holds steady at 8x.
        var steady = Read(provider, frames);
        Assert.Equal(8f, steady[2] - steady[0], 3);
        Assert.Equal(8f, steady[0] - after[(frames - 1) * 2], 3);
    }

    // — J4: box filter beyond 1x —

    [Theory]
    [InlineData("scratch", 5000.0)]
    [InlineData("stems", 5000.0)]
    [InlineData("scratch", 7000.0)]
    [InlineData("stems", 7000.0)]
    public void At_8x_a_tone_above_the_output_Nyquist_is_filtered_far_below_plain_interpolation(string kind, double hz)
    {
        // At 8x a 5 kHz / 7 kHz source tone plays at 40 / 56 kHz, beyond the 24 kHz
        // output Nyquist; plain two-point interpolation folds it back at full level.
        var (filtered, plain) = EnergyAt8x(kind, hz);
        Assert.True(filtered < 0.1 * plain, $"filtered energy {filtered} vs plain {plain}");
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public void At_8x_a_tone_below_the_output_Nyquist_passes(string kind)
    {
        var (filtered, plain) = EnergyAt8x(kind, 200.0);
        Assert.True(filtered > 0.9 * plain, $"filtered energy {filtered} vs plain {plain}");
    }

    /// <summary>Mean-square level of a sine tone read at a steady 8x, through the
    /// provider and through plain two-point interpolation at the same positions.</summary>
    private static (double Filtered, double Plain) EnergyAt8x(string kind, double hz)
    {
        const int frames = 2048;
        const int skip = 4; // the first output frame's box reaches before the track start
        var source = Stereo(frames * 8 + 64, f => (float)Math.Sin(2 * Math.PI * hz * f / SampleRate));
        var provider = Make(kind, source);
        SetSpeed(provider, 8f);
        var output = Read(provider, frames);

        double filtered = 0, plain = 0;
        for (int i = skip; i < frames; i++)
        {
            double position = i * 8.0;
            int iFrame = (int)Math.Floor(position);
            float frac = (float)(position - iFrame);
            float la = source[iFrame * 2], lb = source[iFrame * 2 + 2];
            float reference = la + frac * (lb - la);
            plain += reference * reference;
            filtered += output[i * 2] * output[i * 2];
        }
        return (filtered / (frames - skip), plain / (frames - skip));
    }

    // — J5: steady speeds up to 1x read exactly as before —

    [Theory]
    [MemberData(nameof(Providers))]
    public void Steady_1x_reads_the_source_samples_exactly(string kind)
    {
        var source = Noise(10_000);
        var provider = Make(kind, source);
        var first = Read(provider, 512);
        var second = Read(provider, 512);
        for (int i = 0; i < first.Length; i++)
        {
            Assert.Equal(source[i], first[i]);
            Assert.Equal(source[first.Length + i], second[i]);
        }
    }

    [Theory]
    [InlineData("scratch", 0.5f)]
    [InlineData("stems", 0.5f)]
    [InlineData("scratch", 0.73f)]
    [InlineData("stems", 0.73f)]
    public void Steady_forward_speed_below_1x_is_plain_two_point_interpolation(string kind, float speed)
    {
        var source = Noise(10_000);
        var provider = Make(kind, source);
        SetSpeed(provider, speed);
        var output = new List<float>();
        output.AddRange(Read(provider, 512));
        output.AddRange(Read(provider, 512));

        double srcFrame = 0;
        for (int of = 0; of < 1024; of++)
        {
            AssertPlainFrame(source, srcFrame, output, of);
            srcFrame += speed;
        }
    }

    [Theory]
    [InlineData("scratch", -0.5f)]
    [InlineData("stems", -0.5f)]
    [InlineData("scratch", -1f)]
    [InlineData("stems", -1f)]
    public void Steady_reverse_speed_up_to_1x_is_plain_two_point_interpolation(string kind, float speed)
    {
        var source = Noise(10_000);
        var provider = Make(kind, source);
        provider.Seek(8_000 * 2);
        SetSpeed(provider, speed);
        var output = new List<float>();
        output.AddRange(Read(provider, 512));
        output.AddRange(Read(provider, 512));

        // The seek declick crossfades the first 256 frames; compare after it.
        const int fadeFrames = 256;
        double srcFrame = 8_000;
        for (int of = 0; of < 1024; of++)
        {
            if (of >= fadeFrames) AssertPlainFrame(source, srcFrame, output, of);
            srcFrame += speed;
        }
    }

    private static void AssertPlainFrame(float[] source, double srcFrame, List<float> output, int of)
    {
        int iFrame = (int)Math.Floor(srcFrame);
        float frac = (float)(srcFrame - iFrame);
        int idx = iFrame * 2;
        float left = source[idx] + frac * (source[idx + 2] - source[idx]);
        float right = source[idx + 1] + frac * (source[idx + 3] - source[idx + 1]);
        Assert.Equal(left, output[of * 2]);
        Assert.Equal(right, output[of * 2 + 1]);
    }

    private static float[] Noise(int frames)
    {
        var random = new Random(130);
        var data = new float[frames * 2];
        for (int i = 0; i < data.Length; i++) data[i] = (float)(random.NextDouble() * 2 - 1);
        return data;
    }
}
