namespace Sholto.Interface.Bench.Tests.Rendering;

/// <summary>Bench's rendered WAV must be the source, not a running sum of it. Regression for
/// <c>OfflineRenderer.RenderFrames</c> reusing its buffer without clearing it (the router adds into it).</summary>
public sealed class OfflineRenderFidelityTests : IDisposable
{
    private const int Seconds = 3;
    private readonly string _dir = Directory.CreateTempSubdirectory("bench-fidelity").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private RenderedWav RenderChirp(int sourceRate, out float[] source)
    {
        var synth = new SyntheticSource(sourceRate, Seconds + 1);
        source = new float[4096].Concat(synth.Mono()).ToArray();   // 4096 of leading silence so lag 0..4096 means "render delayed by lag"
        string src = Path.Combine(_dir, $"src{sourceRate}.wav");
        synth.WriteWav(src);
        return new BenchRenderRig().Render(src, Seconds, Path.Combine(_dir, $"out{sourceRate}.wav"));
    }

    /// <summary>Best normalised correlation of the render against the source over a small lag range,
    /// plus the least-squares gain at that lag, measured on <c>[from, from + len)</c>.</summary>
    private static (double Corr, double Gain) Match(float[] rendered, float[] source, int from, int len)
    {
        double best = -1, bestGain = 0;
        for (int lag = 0; lag <= 4096; lag++)
        {
            double sxy = 0, sxx = 0, syy = 0;
            for (int i = from; i < from + len; i++)
            {
                double x = source[i - lag + 4096], y = rendered[i];
                sxy += x * y; sxx += x * x; syy += y * y;
            }
            double c = sxy / Math.Sqrt(sxx * syy + 1e-30);
            if (c > best) { best = c; bestGain = sxy / (sxx + 1e-30); }
        }
        return (best, bestGain);
    }

    [Fact]
    public void Render_of_a_48k_source_matches_it_with_no_dc_and_no_growth()
    {
        var wav = RenderChirp(48000, out var source);
        var left = wav.Left;
        Assert.True(left.Length >= Seconds * 48000 - 4096);

        // Compare window by window across the render: every window must match the source with the same gain.
        int win = 48000 / 2;
        var first = Match(left, source, from: 8192, len: win);
        Assert.True(first.Corr >= 0.99, $"first window correlation {first.Corr:F4}");
        foreach (int from in new[] { 8192 + win, 8192 + 2 * win, 8192 + 4 * win })
        {
            var m = Match(left, source, from, win);
            Assert.True(m.Corr >= 0.99, $"window @{from} correlation {m.Corr:F4}");
            Assert.InRange(m.Gain / first.Gain, 0.9, 1.1);   // no growth across buffers
        }

        Assert.InRange(first.Gain, 0.05, 1.05);               // never louder than the source
        Assert.True(Math.Abs(left.Average()) < 0.01, $"DC {left.Average():F4}");
        Assert.True(left.Max(Math.Abs) <= 0.55f, $"peak {left.Max(Math.Abs):F3}");
    }

    [Fact]
    public void A_44k1_source_renders_at_the_correct_speed()
    {
        // Played 8.8% fast, a chirp finishes its sweep early and the match against the source collapses.
        var wav = RenderChirp(44100, out var source44);
        var resampled = new float[(int)(source44.Length * 48000L / 44100) + 4096];
        for (int i = 0; i + 4096 < resampled.Length; i++)
        {
            double p = i * 44100.0 / 48000, f = p - Math.Floor(p); int j = (int)p + 4096;   // source44 already has 4096 of lead-in
            if (j + 1 < source44.Length) resampled[i + 4096] = (float)(source44[j] * (1 - f) + source44[j + 1] * f);
        }
        var m = Match(wav.Left, resampled, from: 48000 * 2, len: 24000);
        Assert.True(m.Corr >= 0.99, $"late-window correlation {m.Corr:F4}");
    }

    [Fact]
    public void A_44k1_file_loads_with_the_correct_duration()
    {
        // 2 s of 44.1 kHz audio must end 2 s into the render (96000 frames), within one 4096-frame buffer;
        // un-resampled it ends after 2 * 44100 / 48000 = 1.8375 s (88200 frames).
        var synth = new SyntheticSource(44100, 2.0);
        string src = Path.Combine(_dir, "dur.wav");
        synth.WriteWav(src);
        var wav = new BenchRenderRig().Render(src, 3.0, Path.Combine(_dir, "dur-out.wav"));

        int last = Array.FindLastIndex(wav.Left, x => Math.Abs(x) > 0.01f);
        Assert.InRange(last, 96000 - 4096, 96000 + 4096);
    }
}
