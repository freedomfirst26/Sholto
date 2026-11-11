using Sholto.App.Audio;
using SoundFlow.Abstracts;
using SoundFlow.Enums;
using SoundFlow.Structs;

namespace Sholto.App.Audio.Tests;

public sealed class EffectMixTests
{
    private const int Rate = 48000;
    private const int Block = 64;
    private const int Ramp = 240;
    private const int Settled = Ramp + Block; // frames after which a Mix ramp is complete
    private const float Floor = 3.1623e-5f;   // -90 dBFS

    private static readonly AudioFormat Format = new() { SampleRate = Rate, Channels = 2, Format = SampleFormat.F32 };

    /// <summary>A chain effect set up so it audibly changes a tone, after a 0.5 s Tone pre-roll
    /// (fills echo's delay and finishes roll's capture pass). Returns the effect and the cursor.</summary>
    private static (DeckEffect effect, int cursor) NewPrimed(int index)
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
        Run([effect], Secs(0.5), Tone, ref cursor);
        return (effect, cursor);
    }

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

    private static float MaxAbs(float[] x, int fromFrame = 0)
    {
        float m = 0;
        for (int i = fromFrame * 2; i < x.Length; i++) m = MathF.Max(m, MathF.Abs(x[i]));
        return m;
    }

    /// <summary>Largest |x[i] - x[i-1]| per channel.</summary>
    private static float MaxStep(float[] x, int fromFrame = 0)
    {
        float m = 0;
        for (int ch = 0; ch < 2; ch++)
            for (int i = Math.Max(1, fromFrame); i < x.Length / 2; i++)
                m = MathF.Max(m, MathF.Abs(x[i * 2 + ch] - x[(i - 1) * 2 + ch]));
        return m;
    }

    private static float MaxDiff(float[] a, float[] b)
    {
        float m = 0;
        for (int i = 0; i < a.Length; i++) m = MathF.Max(m, MathF.Abs(a[i] - b[i]));
        return m;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Mix_param_id_sets_only_mix_on_every_effect(int index)
    {
        var effect = (DeckEffect)new DeckEffectFactory(new TempoMath()).Chain[index](null!, Format);
        var row = Assert.Single(effect.Params.ToArray(), p => p.Id == DeckEffect.MixParamId);
        Assert.Equal("Mix", row.Name);
        Assert.Equal(0.0, row.Min);
        Assert.Equal(1.0, row.Max);
        Assert.Equal(1.0, row.Default);

        effect.SetParam(DeckEffect.MixParamId, 0.25);
        int cursor = 0;
        var o = Run([effect], Secs(0.3), Tone, ref cursor);
        Assert.Equal(Input(Secs(0.3), 0, Tone), o);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Mix_one_is_bit_identical_to_an_untouched_twin(int index)
    {
        var (a, ca) = NewPrimed(index);
        var (b, cb) = NewPrimed(index);
        b.SetParam(DeckEffect.MixParamId, 1);
        int n = Secs(1.0), start = ca;
        var oa = Run([a], n, Tone, ref ca);
        var ob = Run([b], n, Tone, ref cb);
        Assert.Equal(oa, ob);
        Assert.True(MaxDiff(oa, Input(n, start, Tone)) > 0.01f, $"{a.EffectId}: effect was a no-op");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Mix_zero_settled_is_exact_bypass_and_resumes_without_a_jump(int index)
    {
        var (a, ca) = NewPrimed(index);
        var (twin, ct) = NewPrimed(index);
        a.SetParam(DeckEffect.MixParamId, 0);
        Run([a], Secs(0.1), Tone, ref ca);
        Run([twin], Secs(0.1), Tone, ref ct);

        int start = ca;
        var bypass = Run([a], Secs(0.1), Tone, ref ca);
        Assert.Equal(Input(Secs(0.1), start, Tone), bypass);
        Run([twin], Secs(0.1), Tone, ref ct);

        a.SetParam(DeckEffect.MixParamId, 1);
        start = ca;
        var resumed = Run([a], Secs(0.1), Tone, ref ca);
        var wet = Run([twin], Secs(0.1), Tone, ref ct);
        var dry = Input(Secs(0.1), start, Tone);
        Assert.True(MaxDiff(wet, dry) > 0.01f, $"{a.EffectId}: effect was a no-op");
        // A resume that jumps instead of ramping steps by about the full 0.5 amplitude; the 4 x 0.5
        // allows a wet transient up to 3x the input peak from EQ/filter's cleared state.
        float bound = MathF.Max(MaxStep(wet), MaxStep(dry)) + 4f * 0.5f / Ramp + 1e-4f;
        Assert.True(MaxStep(resumed) <= bound, $"{a.EffectId}: resume step {MaxStep(resumed)} > {bound}");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Mix_param_clamps_and_ignores_nan(int index)
    {
        var (a, ca) = NewPrimed(index);
        var (twin, ct) = NewPrimed(index);
        a.SetParam(DeckEffect.MixParamId, 1.5);
        int n = Secs(0.3);
        Assert.Equal(Run([twin], n, Tone, ref ct), Run([a], n, Tone, ref ca));

        a.SetParam(DeckEffect.MixParamId, -0.2);
        int start = ca;
        var o = Run([a], n, Tone, ref ca);
        Assert.Equal(Input(n - Settled, start + Settled, Tone), o[(Settled * 2)..]);

        a.SetParam(DeckEffect.MixParamId, 0);
        a.SetParam(DeckEffect.MixParamId, double.NaN);
        start = ca;
        o = Run([a], n, Tone, ref ca);
        Assert.Equal(Input(n - Settled, start + Settled, Tone), o[(Settled * 2)..]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Mix_sweep_has_no_step_beyond_the_derived_bound(int index)
    {
        var (a, ca) = NewPrimed(index);
        var (w, cw) = NewPrimed(index);
        const int blocks = 37;
        int start = ca;
        var parts = new List<float[]>();
        var wparts = new List<float[]>();
        for (int k = 0; k < blocks; k++)
        {
            a.SetParam(DeckEffect.MixParamId, Math.Abs(1.0 - 2.0 * k / blocks));
            parts.Add(Run([a], Block, Tone, ref ca));
            wparts.Add(Run([w], Block, Tone, ref cw));
        }
        a.SetParam(DeckEffect.MixParamId, 1);
        parts.Add(Run([a], Secs(0.1), Tone, ref ca));
        wparts.Add(Run([w], Secs(0.1), Tone, ref cw));

        var swept = parts.SelectMany(p => p).ToArray();
        var wet = wparts.SelectMany(p => p).ToArray();
        var dry = Input(swept.Length / 2, start, Tone);
        float bound = MathF.Max(MaxStep(wet), MaxStep(dry)) + MaxDiff(wet, dry) / Ramp + 1e-4f;
        Assert.True(MaxDiff(wet, dry) > 0.01f, $"{a.EffectId}: effect was a no-op");
        Assert.True(MaxStep(swept) <= bound, $"{a.EffectId}: step {MaxStep(swept)} > bound {bound}");
    }

    [Fact]
    public void Echo_tail_survives_mix_zero_and_back()
    {
        var a = (DeckEffect)new DeckEffectFactory(new TempoMath()).Chain[2](null!, Format);
        var b = (DeckEffect)new DeckEffectFactory(new TempoMath()).Chain[2](null!, Format);
        Assert.Equal("echo", a.EffectId);
        a.SetParam(0, 1); b.SetParam(0, 1);
        int ca = 0, cb = 0;
        Run([a], Secs(1.0), Tone, ref ca);
        Run([b], Secs(1.0), Tone, ref cb);
        a.SetParam(0, 0); b.SetParam(0, 0);

        a.SetParam(DeckEffect.MixParamId, 0);
        var silent = Run([a], Secs(0.1), Silence, ref ca);
        Assert.True(MaxAbs(silent, Settled) < Floor);
        a.SetParam(DeckEffect.MixParamId, 1);
        var oa = Run([a], Secs(0.3), Silence, ref ca);
        var ob = Run([b], Secs(0.4), Silence, ref cb);

        var tailA = oa[(Settled * 2)..];
        var tailB = ob[((Secs(0.1) + Settled) * 2)..];
        Assert.Equal(tailB, tailA);
        Assert.True(MaxAbs(tailB) > 0.01f, $"tail peak {MaxAbs(tailB)}");
    }
}
