using System.Diagnostics;
using Sholto.App.Audio;
using SoundFlow.Abstracts;
using SoundFlow.Enums;
using SoundFlow.Structs;
using Xunit.Abstractions;

namespace Sholto.App.Audio.Tests;

public sealed class EffectResetTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    private const int Rate = 48000;
    private const int Block = 64;
    private const int Ramp = 240;
    private const int Settled = Ramp + Block; // frames after which a reset is complete
    private const float Floor = 3.1623e-5f;   // -90 dBFS

    private static readonly AudioFormat Format = new() { SampleRate = Rate, Channels = 2, Format = SampleFormat.F32 };

    private static EchoEffect NewEcho() => new(null!, Format, new TempoMath(), new EffectOutputStage(Format));
    private static BeatRepeatEffect NewRoll() => new(null!, Format, new TempoMath(), new EffectOutputStage(Format));

    /// <summary>Feeds a source through a chain of modifiers in fixed 64-frame
    /// stereo blocks and returns the interleaved result.</summary>
    private static float[] Run(IReadOnlyList<SoundModifier> chain, int frames, Func<int, float> source, ref int frameCursor, int block = Block)
    {
        var result = new float[frames * 2];
        var buf = new float[block * 2];
        int done = 0;
        while (done < frames)
        {
            int n = Math.Min(block, frames - done);
            var span = buf.AsSpan(0, n * 2);
            for (int i = 0; i < n; i++)
            {
                float s = source(frameCursor + done + i);
                span[i * 2] = s; span[i * 2 + 1] = s;
            }
            foreach (var m in chain) m.Process(span, 2);
            span.CopyTo(result.AsSpan(done * 2));
            done += n;
        }
        frameCursor += frames;
        return result;
    }

    private static float Tone(int frame) => 0.5f * MathF.Sin(2f * MathF.PI * 440f * frame / Rate);
    private static float Silence(int frame) => 0f;

    private static float[] Input(int frames, int start, Func<int, float> source)
    {
        var r = new float[frames * 2];
        for (int i = 0; i < frames; i++) { r[i * 2] = source(start + i); r[i * 2 + 1] = r[i * 2]; }
        return r;
    }

    private static int Secs(double s) => (int)(s * Rate);

    private static float MaxAbs(float[] x, int fromFrame = 0, int toFrame = int.MaxValue)
    {
        float m = 0;
        int end = Math.Min(x.Length, toFrame == int.MaxValue ? x.Length : toFrame * 2);
        for (int i = fromFrame * 2; i < end; i++) m = MathF.Max(m, MathF.Abs(x[i]));
        return m;
    }

    [Fact]
    public void Reset_is_deferred_to_process_and_fades_without_a_step()
    {
        var echo = NewEcho();
        var twin = NewEcho();
        int cursor = 0, twinCursor = 0;
        echo.SetEnabled(true); twin.SetEnabled(true);
        Run([echo], Secs(1.0), Tone, ref cursor);
        Run([twin], Secs(1.0), Tone, ref twinCursor);
        echo.SetEnabled(false); twin.SetEnabled(false);

        echo.RequestReset();
        var o = Run([echo], Secs(0.5), Silence, ref cursor);
        var t = Run([twin], Secs(0.5), Silence, ref twinCursor);

        Assert.NotEqual(0f, o[0]); // tail not cleared on the calling thread
        for (int i = 0; i < Ramp; i++)
        {
            float bound = MathF.Abs(t[i * 2]) * (1f - (float)i / Ramp) + 1e-6f;
            Assert.True(MathF.Abs(o[i * 2]) <= bound, $"frame {i}: {o[i * 2]} > {bound}");
            Assert.True(MathF.Abs(o[i * 2 + 1]) <= bound, $"frame {i} R: {o[i * 2 + 1]} > {bound}");
        }
        Assert.True(MaxAbs(o, Settled) < Floor);
    }

    [Fact]
    public void Roll_reset_clears_the_captured_slice_and_disengages()
    {
        var roll = NewRoll();
        int cursor = 0;
        roll.SetEnabled(true);
        Run([roll], Secs(0.3), Tone, ref cursor);

        roll.RequestReset();
        var quiet = Run([roll], Secs(0.5), Silence, ref cursor);
        Assert.True(MaxAbs(quiet, Settled) < Floor);

        int start = cursor;
        var o = Run([roll], Secs(0.1), Tone, ref cursor);
        Assert.Equal(Input(Secs(0.1), start, Tone), o);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Every_chain_effect_is_silent_after_reset(int index)
    {
        var effect = (DeckEffect)new DeckEffectFactory(new TempoMath()).Chain[index](null!, Format);
        switch (effect.EffectId)
        {
            case "echo":
            case "roll": effect.SetParam(0, 1); break;
            case "eq3": effect.SetParam(0, 0); break;
            case "filter": effect.SetParam(0, 0.1); break;
            default: Assert.Fail($"unknown effect {effect.EffectId}"); break;
        }
        int cursor = 0;
        Run([effect], Secs(0.3), Tone, ref cursor);
        effect.RequestReset();
        var o = Run([effect], Secs(0.2), Silence, ref cursor);
        for (int i = Settled * 2; i < o.Length; i++)
            Assert.True(o[i] == 0f, $"{effect.EffectId}: sample {i} = {o[i]}");
    }

    [Fact]
    public void Enable_after_request_survives_the_reset()
    {
        var echo = NewEcho();
        echo.RequestReset();
        echo.SetEnabled(true);
        int cursor = 0;
        var o = Run([echo], Secs(1.0), f => f < Secs(0.05) ? Tone(f) : 0f, ref cursor);
        Assert.True(MaxAbs(o, 11250, 13650) > Floor);
    }

    [Fact]
    public void Repeated_request_during_fade_out_is_absorbed()
    {
        var eq = new BiquadEq3Band(null!, Format, new EffectOutputStage(Format));
        int cursor = 0;
        eq.RequestReset();
        Run([eq], 100, Tone, ref cursor);
        eq.RequestReset();
        int rest = Secs(0.1);
        int start = cursor;
        var o = Run([eq], rest, Tone, ref cursor);
        var expected = Input(rest, start, Tone);
        // One fade-out and one fade-in only: everything from 240 + 240 + 64 on is the input.
        int from = (Ramp + Ramp + Block) - start;
        for (int i = from * 2; i < o.Length; i++) Assert.Equal(expected[i], o[i]);
        Assert.Equal(expected[^(Block * 2)..], o[^(Block * 2)..]);
    }

    [Fact]
    public void Request_during_fade_in_fades_out_without_a_jump()
    {
        var eq = new BiquadEq3Band(null!, Format, new EffectOutputStage(Format));
        int cursor = 0;
        eq.RequestReset();
        var a = Run([eq], 320, _ => 0.5f, ref cursor);
        eq.RequestReset();
        var b = Run([eq], Secs(0.1), _ => 0.5f, ref cursor);
        var all = a.Concat(b).ToArray();
        float limit = 0.5f / Ramp + 1e-6f;
        for (int ch = 0; ch < 2; ch++)
            for (int i = 1; i < all.Length / 2; i++)
                Assert.True(MathF.Abs(all[i * 2 + ch] - all[(i - 1) * 2 + ch]) <= limit, $"jump at frame {i}");
        for (int i = all.Length - Block * 2; i < all.Length; i++) Assert.Equal(0.5f, all[i]);
    }

    [Fact]
    public void Request_before_first_process_is_applied()
    {
        var filter = new DjFilter(null!, Format, new EffectOutputStage(Format));
        filter.RequestReset();
        int cursor = 0;
        int n = Secs(0.1);
        var o = Run([filter], n, Tone, ref cursor);
        var expected = Input(n, 0, Tone);
        Assert.Equal(expected[^(Block * 2)..], o[^(Block * 2)..]);
        Assert.True(MaxAbs(o, 0, 1) < 1f);
    }

    [Fact]
    public void Reset_state_cost_is_measured()
    {
        var echo = NewEcho();
        var roll = NewRoll();
        var buf = new float[Block * 2];
        var samples = new List<double>();
        int cursor = 0;
        for (int iter = 0; iter < 100; iter++)
        {
            echo.SetEnabled(true); roll.SetEnabled(true);
            Run([echo, roll], Secs(0.3), Tone, ref cursor);
            echo.RequestReset(); roll.RequestReset();
            double worst = 0;
            for (int b = 0; b < 8; b++)
            {
                Array.Clear(buf);
                long t0 = Stopwatch.GetTimestamp();
                echo.Process(buf, 2);
                roll.Process(buf, 2);
                double us = Stopwatch.GetElapsedTime(t0).TotalMicroseconds;
                worst = Math.Max(worst, us);
            }
            samples.Add(worst);
        }
        samples.Sort();
        _output.WriteLine($"echo+roll ResetState buffer cost: median {samples[samples.Count / 2]:F0} us, max {samples[^1]:F0} us");

        var o = Run([echo, roll], Secs(0.1), Silence, ref cursor);
        Assert.True(MaxAbs(o) == 0f);
    }

    private static (DeckMixer mixer, IReadOnlyList<SoundModifier> chain) NewMixer()
    {
        var chain = new DeckEffectFactory(new TempoMath()).Chain.Select(f => f(null!, Format)).ToList();
        var mixer = new DeckMixer(() => new TrackAnalysis(), () => 1f);
        mixer.AttachModifiers(chain);
        return (mixer, chain);
    }

    [Fact]
    public void Mixer_reset_clears_echo_and_roll_through_the_whole_chain()
    {
        var (mixer, chain) = NewMixer();
        mixer.SetEcho(true);
        mixer.SetParam("roll", 0, 1);
        Assert.True(mixer.EchoActive);
        int cursor = 0;
        Run(chain, Secs(0.5), Tone, ref cursor);

        mixer.ResetEffects();
        Assert.False(mixer.EchoActive);
        var quiet = Run(chain, Secs(0.5), Silence, ref cursor);
        Assert.True(MaxAbs(quiet, Settled) < Floor, $"peak after reset {MaxAbs(quiet, Settled)}");

        int start = cursor;
        var o = Run(chain, Secs(0.1), Tone, ref cursor);
        var expected = Input(Secs(0.1), start, Tone);
        for (int i = o.Length - Block * 2; i < o.Length; i++) Assert.Equal(expected[i], o[i], 1e-4f);
    }
}
