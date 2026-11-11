using SoundFlow.Enums;
using SoundFlow.Structs;
using Xunit;

namespace Sholto.App.Audio.Tests;

public sealed class EffectOutputStageTests
{
    private const int Block = 64;
    private const int Ramp = 240;

    private static readonly AudioFormat Format = new() { SampleRate = 48000, Channels = 2, Format = SampleFormat.F32 };

    private static float[] Run(EffectOutputStage stage, FakeEffectDsp dsp, int blocks, float input = 0.5f)
    {
        var result = new float[blocks * Block * 2];
        for (int b = 0; b < blocks; b++)
        {
            var span = result.AsSpan(b * Block * 2, Block * 2);
            span.Fill(input);
            stage.Process(span, 2, dsp);
        }
        return result;
    }

    [Fact]
    public void Settled_mix_one_is_exactly_the_dsp_output()
    {
        var stage = new EffectOutputStage(Format);
        var dsp = new FakeEffectDsp();
        var o = Run(stage, dsp, 10);
        Assert.All(o, v => Assert.Equal(1f, v));
        Assert.Equal(10, dsp.ProcessCount);
    }

    [Fact]
    public void Settled_mix_zero_with_skip_bypasses_dsp_and_resets_once()
    {
        var stage = new EffectOutputStage(Format);
        var dsp = new FakeEffectDsp { Skip = true };
        stage.SetMix(0);
        Run(stage, dsp, 10);                       // ramp settles
        int before = dsp.ProcessCount;
        var o = Run(stage, dsp, 5);
        Assert.All(o, v => Assert.Equal(0.5f, v));
        Assert.Equal(before, dsp.ProcessCount);
        Assert.Equal(1, dsp.ResetCount);
    }

    [Fact]
    public void Settled_mix_zero_without_skip_still_runs_dsp()
    {
        var stage = new EffectOutputStage(Format);
        var dsp = new FakeEffectDsp { Skip = false };
        stage.SetMix(0);
        Run(stage, dsp, 10);
        int before = dsp.ProcessCount;
        var o = Run(stage, dsp, 5);
        Assert.All(o, v => Assert.Equal(0.5f, v));
        Assert.Equal(before + 5, dsp.ProcessCount);
        Assert.Equal(0, dsp.ResetCount);
    }

    [Fact]
    public void Reset_fades_to_zero_runs_reset_once_and_returns()
    {
        var stage = new EffectOutputStage(Format);
        var dsp = new FakeEffectDsp();
        stage.RequestReset();
        var o = Run(stage, dsp, 20);
        Assert.Equal(1, dsp.ResetCount);

        int zero = -1;
        for (int i = 0; i < o.Length / 2; i++)
            if (o[i * 2] == 0f) { zero = i; break; }
        Assert.InRange(zero, Ramp - 1, Ramp + 1);
        for (int i = 1; i <= zero; i++) Assert.True(o[i * 2] <= o[(i - 1) * 2]);
        int back = Ramp + Ramp;
        for (int i = Ramp; i < back; i++) Assert.True(o[(i + 1) * 2] >= o[i * 2]);
        for (int i = (Ramp + Ramp + Block) * 2; i < o.Length; i++) Assert.Equal(1f, o[i]);
    }

    [Theory]
    [InlineData(double.NaN, 1f)]
    [InlineData(5.0, 1f)]
    [InlineData(-1.0, 0.5f)]
    public void Nan_mix_is_ignored_and_mix_clamps(double mix, float expected)
    {
        var stage = new EffectOutputStage(Format);
        var dsp = new FakeEffectDsp();
        stage.SetMix(mix);
        Run(stage, dsp, 10);                       // settle any ramp
        var o = Run(stage, dsp, 2);
        Assert.All(o, v => Assert.Equal(expected, v));
    }
}
