using Microsoft.Extensions.Options;
using Sholto.App.Analysis.Stems;
using Xunit;

namespace Sholto.App.Audio.Tests;

/// <summary>
/// Bit-exact characterization of <see cref="StemMixDataProvider.ReadBytes"/>: a 64-bit
/// FNV-1a hash over the output float bits for fixed scenarios (seek declick, loop-wrap
/// crossfade, varispeed). Any change to the audio math or to the order of its operations
/// changes the hash. The golden fixtures never play stems, so this is the only gate.
/// </summary>
public class StemMixOutputPinTests
{
    private const int TrackFrames = 48_000;
    private const int BufferFrames = 64;

    // Captured on the unsplit provider.
    private const ulong ExpectedMixed = 0xBDEFC5A27375E189UL;
    private const ulong ExpectedShortLoop = 0x4FB980A71394846CUL;

    [Fact]
    public void Seek_declick_loop_wraps_and_varispeed_produce_the_pinned_output()
    {
        var provider = Build();
        provider.SetGain(StemMixDataProvider.Drums, 0.5f);
        provider.SetGain(StemMixDataProvider.Vocals, 1f);
        provider.SetGain(StemMixDataProvider.Bass, 0.25f);
        provider.SetGain(StemMixDataProvider.Other, 0.75f);
        var hash = new Fnv();
        var buffer = new float[BufferFrames * 2];

        provider.BuildLoopTail(41_200);
        provider.SetLoop(new LoopRegion(40_000, 41_200));      // 600 frames
        provider.Seek(39_000);
        Reads(provider, buffer, hash, 80);
        provider.SetSpeed(1.37f);
        Reads(provider, buffer, hash, 40);
        provider.Seek(60_000);                                  // outside the loop
        Reads(provider, buffer, hash, 10);
        provider.SetSpeed(1f);
        provider.Seek(40_100);                                  // back inside the loop
        Reads(provider, buffer, hash, 30);
        provider.SetSpeed(-0.8f);
        provider.SetLoop(null);
        Reads(provider, buffer, hash, 10);

        Assert.True(ExpectedMixed == hash.Value, $"actual hash 0x{hash.Value:X16}");
    }

    [Fact]
    public void A_loop_shorter_than_a_buffer_wraps_many_times_per_read_with_the_pinned_output()
    {
        var provider = Build();
        var hash = new Fnv();
        var buffer = new float[BufferFrames * 2];

        provider.BuildLoopTail(20_080);
        provider.SetLoop(new LoopRegion(20_000, 20_080));       // 40 frames, shorter than the fade
        provider.Seek(20_000);
        Reads(provider, buffer, hash, 25);
        provider.Seek(20_000);                                  // seek while a wrap crossfade is running
        Reads(provider, buffer, hash, 25);

        Assert.True(ExpectedShortLoop == hash.Value, $"actual hash 0x{hash.Value:X16}");
    }

    private static StemMixDataProvider Build()
    {
        var stems = new float[4][];
        for (int s = 0; s < 4; s++)
        {
            stems[s] = new float[TrackFrames * 2];
            for (int i = 0; i < stems[s].Length; i++)
                stems[s][i] = ((i * (s + 3)) % 997) / 997f - 0.5f;
        }
        return new StemMixDataProvider(
            new StemSamples(stems[0], stems[1], stems[2], stems[3]), 48000,
            Options.Create(new LoopOptions()));
    }

    private static void Reads(StemMixDataProvider provider, float[] buffer, Fnv hash, int count)
    {
        for (int r = 0; r < count; r++)
        {
            int produced = provider.ReadBytes(buffer);
            hash.Add(produced);
            for (int i = 0; i < produced; i++) hash.Add(BitConverter.SingleToInt32Bits(buffer[i]));
        }
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
