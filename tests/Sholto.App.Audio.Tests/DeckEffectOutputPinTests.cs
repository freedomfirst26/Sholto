using SoundFlow.Enums;
using SoundFlow.Structs;
using Xunit;

namespace Sholto.App.Audio.Tests;

/// <summary>
/// Bit-exact characterization of each chain effect's output through the Mix blend and the
/// reset fade: a 64-bit FNV-1a hash over the output float bits for a fixed scenario (Mix
/// ramp, reset mid-ramp, settled Mix 0, settled Mix 1 in 1024-frame blocks, reset inside a
/// long block). Any change to the blend or fade maths, or to the order of its operations,
/// changes the hash. The golden fixtures cover eq, filter and echo but never Mix or roll.
/// </summary>
public class DeckEffectOutputPinTests
{
    private const int Rate = 48000;
    private const int Block = 64;
    private const int LongBlock = 1024;

    private static readonly AudioFormat Format = new() { SampleRate = Rate, Channels = 2, Format = SampleFormat.F32 };

    // Captured on DeckEffect before C205.
    private static readonly ulong[] Expected =
    [
        0xF2E2CA0AAA5EE225UL, // eq3
        0x422D8448B736FF1DUL, // filter
        0xD8D60E067C49259DUL, // echo
        0x24C5CECF85F27BE1UL, // roll
    ];

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Chain_effect_output_through_mix_and_reset_is_pinned(int index)
    {
        var effect = (DeckEffect)new DeckEffectFactory(new TempoMath()).Chain[index](null!, Format);
        var hash = new Fnv();
        int cursor = 0;

        SetDsp(effect);
        Run(effect, Secs(0.5), Block, hash, ref cursor);

        effect.SetParam(DeckEffect.MixParamId, 0.3);
        Run(effect, 2 * Block, Block, hash, ref cursor);
        effect.RequestReset();                                   // mid-ramp
        SetDsp(effect);
        Run(effect, Secs(0.05) - 2 * Block, Block, hash, ref cursor);

        effect.SetParam(DeckEffect.MixParamId, 0.0);
        Run(effect, Secs(0.1), Block, hash, ref cursor);

        effect.SetParam(DeckEffect.MixParamId, 1.0);
        Run(effect, Secs(0.1), LongBlock, hash, ref cursor);

        effect.RequestReset();                                   // fade reaches 0 mid-buffer
        SetDsp(effect);
        Run(effect, Secs(0.1), LongBlock, hash, ref cursor);

        Assert.True(Expected[index] == hash.Value, $"actual 0x{hash.Value:X16}");
    }

    private static void SetDsp(DeckEffect effect)
    {
        switch (effect.EffectId)
        {
            case "echo":
            case "roll": effect.SetParam(0, 1); break;
            case "eq3": effect.SetParam(0, 0); break;
            case "filter": effect.SetParam(0, 0.1); break;
            default: Assert.Fail($"unknown effect {effect.EffectId}"); break;
        }
    }

    private static int Secs(double s) => (int)(s * Rate);

    private static float Tone(int frame) => 0.5f * MathF.Sin(2f * MathF.PI * 440f * frame / Rate);

    private static void Run(DeckEffect effect, int frames, int block, Fnv hash, ref int cursor)
    {
        var buf = new float[block * 2];
        int done = 0;
        while (done < frames)
        {
            int n = Math.Min(block, frames - done);
            var span = buf.AsSpan(0, n * 2);
            for (int i = 0; i < n; i++)
            {
                float s = Tone(cursor + done + i);
                span[i * 2] = s; span[i * 2 + 1] = s;
            }
            effect.Process(span, 2);
            for (int i = 0; i < span.Length; i++) hash.Add(BitConverter.SingleToInt32Bits(span[i]));
            done += n;
        }
        cursor += frames;
    }

    private sealed class Fnv
    {
        public ulong Value { get; private set; } = 14695981039346656037UL;

        public void Add(int v)
        {
            for (int b = 0; b < 4; b++)
            {
                Value ^= (byte)(v >> (8 * b));
                Value *= 1099511628211UL;
            }
        }
    }
}
