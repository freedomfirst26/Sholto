using System.Diagnostics;
using Sholto.App.Audio;
using Xunit;

namespace Sholto.App.Audio.Tests;

public class Mp3DecodeSelectionTests
{
    private static AudioFileDecoder Compose(IAudioDecodeStrategy preferred, IAudioDecodeStrategy fallback) =>
        new([new FallbackDecodeStrategy(preferred, fallback)]);

    [Fact]
    public void Mp3_UsesPreferredWhenItSucceeds()
    {
        var ffmpeg = new StubDecodeStrategy(1f);
        var nlayer = new StubDecodeStrategy(2f);
        float[] pcm = Compose(ffmpeg, nlayer).Decode("/x/a.MP3");
        Assert.Equal(1f, pcm[0]);
        Assert.Equal(0, nlayer.Calls);
    }

    [Fact]
    public void Mp3_FallsBackWhenFfmpegFails()
    {
        var ffmpeg = new StubDecodeStrategy(1f, fails: true);
        var nlayer = new StubDecodeStrategy(2f);
        float[] pcm = Compose(ffmpeg, nlayer).Decode("/x/a.mp3");
        Assert.Equal(2f, pcm[0]);
        Assert.Equal(1, ffmpeg.Calls);
    }

    [Fact]
    public void Mp3_FallsBackWhenFfmpegBinaryMissing()
    {
        var ffmpeg = new FfmpegDecodeStrategy("/nonexistent/ffmpeg-xyz");
        var nlayer = new StubDecodeStrategy(2f);
        float[] pcm = Compose(ffmpeg, nlayer).Decode("/x/a.mp3");
        Assert.Equal(2f, pcm[0]);
    }

    [Fact]
    public void RealMp3_FfmpegMatchesNLayer()
    {
        string mp3 = Environment.GetEnvironmentVariable("SHOLTO_TEST_MP3") ?? "";
        if (!File.Exists(mp3)) return; // opt-in: needs a real MP3 and ffmpeg
        var nlayer = new Mp3DecodeStrategy(new NAudioDecoding());
        var sw = Stopwatch.StartNew();
        float[] a = nlayer.Decode(mp3);
        long nlayerMs = sw.ElapsedMilliseconds;
        sw.Restart();
        float[] b = new FfmpegDecodeStrategy("ffmpeg").Decode(mp3);
        long ffmpegMs = sw.ElapsedMilliseconds;
        Console.WriteLine($"nlayer {nlayerMs} ms ({a.Length} samples), ffmpeg {ffmpegMs} ms ({b.Length} samples)");
        // NLayer's resample tail comes up short of the container duration; ffmpeg matches it.
        int frameDelta = Math.Abs(b.Length / 2 - a.Length / 2);
        Console.WriteLine($"frame delta {frameDelta}");
        Assert.InRange(frameDelta, 0, 4 * 1152);

        // Resampler differences (NAudio vs swr) put the floor near -45 dBFS, not -60. Align by searching the lag (in frames) that minimises the difference on a mid-track window.
        int n = 48000 * 2, start = 48000 * 2 * 30;
        double best = double.MaxValue; int bestLag = 0;
        for (int lag = -4000; lag <= 4000; lag++)
        {
            double e = 0;
            for (int i = 0; i < n; i += 7)
            {
                double d = a[start + i] - b[start + i + lag * 2];
                e += d * d;
            }
            if (e < best) { best = e; bestLag = lag; }
        }
        double err = 0;
        for (int i = 0; i < n; i++)
        {
            double d = a[start + i] - b[start + i + bestLag * 2];
            err += d * d;
        }
        double diffDb = 10 * Math.Log10(err / n + 1e-20);
        Console.WriteLine($"lag {bestLag} frames, diff rms {diffDb:F1} dBFS");
        Assert.True(diffDb < -40, $"diff {diffDb} dBFS");
    }
}
