using System.Diagnostics;
using Xunit;

namespace Sholto.Audio.Tests;

public class FfmpegDecodeStrategyTests
{
    private static readonly bool FfmpegAvailable = CheckFfmpegAvailable();

    private static bool CheckFfmpegAvailable()
    {
        try
        {
            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName = "ffmpeg",
                ArgumentList = { "-version" },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            });
            proc!.WaitForExit(5000);
            return proc.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    [Fact]
    public void CanDecode_M4aTrue_Mp3False()
    {
        var strategy = new FfmpegDecodeStrategy();
        Assert.True(strategy.CanDecode(".m4a"));
        Assert.False(strategy.CanDecode(".mp3"));
    }

    [Fact]
    public void Decode_SineWaveM4a_ProducesExpectedPcm()
    {
        if (!FfmpegAvailable)
        {
            // ffmpeg isn't installed on this machine — skip rather than fail; the
            // strategy needs it at runtime just like the beat detector does.
            return;
        }

        string fixturePath = CreateSineM4aFixture();
        try
        {
            var strategy = new FfmpegDecodeStrategy();
            float[] pcm = strategy.Decode(fixturePath);

            // 2 s @ 48000 Hz stereo = 192000 samples, ±10% for AAC encoder
            // priming/padding.
            int expectedSamples = 2 * AudioFileDecoder.TargetSampleRate * AudioFileDecoder.TargetChannels;
            Assert.InRange(pcm.Length, (int)(expectedSamples * 0.9), (int)(expectedSamples * 1.1));

            int frames = pcm.Length / 2;
            var left = new float[frames];
            var right = new float[frames];
            for (int i = 0; i < frames; i++)
            {
                left[i] = pcm[i * 2];
                right[i] = pcm[i * 2 + 1];
            }

            // Mono source: left and right channels should match closely.
            double maxDiff = 0;
            for (int i = 0; i < frames; i++)
                maxDiff = Math.Max(maxDiff, Math.Abs(left[i] - right[i]));
            Assert.True(maxDiff < 0.05, $"left/right channels differ by up to {maxDiff}, expected near-identical for a mono source");

            // Peak amplitude: ffmpeg's lavfi "sine" source has no amplitude option
            // and emits a fixed low-level signal (measured ~0.13 after this AAC
            // round-trip) rather than a full-scale ±1.0 tone, so the sane range here
            // is "clearly present and not clipped" rather than 0.5-1.0.
            float peak = 0;
            for (int i = 0; i < frames; i++)
                peak = Math.Max(peak, Math.Abs(left[i]));
            Assert.InRange(peak, 0.05f, 1.0f);

            // Dominant frequency via zero-crossing count over the middle second.
            int sampleRate = AudioFileDecoder.TargetSampleRate;
            int midStart = frames / 2 - sampleRate / 2;
            int midEnd = midStart + sampleRate;
            midStart = Math.Max(0, midStart);
            midEnd = Math.Min(frames, midEnd);

            int crossings = 0;
            for (int i = midStart + 1; i < midEnd; i++)
                if (Math.Sign(left[i]) != Math.Sign(left[i - 1]) && left[i - 1] != 0)
                    crossings++;

            // 440 Hz sine => ~880 zero crossings per second.
            Assert.InRange(crossings, 880 * 0.95, 880 * 1.05);
        }
        finally
        {
            File.Delete(fixturePath);
        }
    }

    [Fact]
    public void Decode_UnsupportedExtension_ThrowsNotSupported()
    {
        Assert.Throws<NotSupportedException>(() => AudioFileDecoder.Decode("/tmp/made-up-file.xyzzy"));
    }

    [Fact]
    public void Decode_NonexistentM4aPath_ThrowsWithFfmpegInMessage()
    {
        if (!FfmpegAvailable)
        {
            return;
        }

        var ex = Assert.Throws<InvalidOperationException>(
            () => AudioFileDecoder.Decode("/tmp/sholto_does_not_exist_xyz.m4a"));
        Assert.Contains("ffmpeg", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateSineM4aFixture()
    {
        string path = Path.Combine(Path.GetTempPath(), $"sholto_test_sine_{Guid.NewGuid():N}.m4a");
        var psi = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add("-y");
        psi.ArgumentList.Add("-f");
        psi.ArgumentList.Add("lavfi");
        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add("sine=frequency=440:sample_rate=44100:duration=2");
        psi.ArgumentList.Add("-c:a");
        psi.ArgumentList.Add("aac");
        psi.ArgumentList.Add("-b:a");
        psi.ArgumentList.Add("128k");
        psi.ArgumentList.Add(path);

        using var proc = Process.Start(psi)!;
        proc.WaitForExit();
        if (proc.ExitCode != 0)
        {
            string stderr = proc.StandardError.ReadToEnd();
            throw new InvalidOperationException($"Failed to generate test fixture: {stderr}");
        }
        return path;
    }
}
