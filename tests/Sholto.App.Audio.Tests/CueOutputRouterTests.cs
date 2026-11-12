using Sholto.TestSupport.Allocation;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Enums;
using SoundFlow.Structs;
using Xunit;

namespace Sholto.App.Audio.Tests;

public class CueOutputRouterTests
{
    private readonly CueOutputRouter _router = new(
        new MiniAudioEngine(),
        new AudioFormat { SampleRate = 48000, Channels = 2, Format = SampleFormat.F32 },
        [],
        timingEnabled: false);

    // One stereo frame [L=1, R=2] for brevity.
    private static float[] Stereo(float l, float r) => [l, r];

    [Fact]
    public void FourChannel_MasterOnCh12_CueOnCh34()
    {
        var buf = new float[4]; // 1 frame, 4 channels
        _router.MixDeckInto(buf, Stereo(1f, 2f), frames: 1, channels: 4,
            masterGain: 0.5f, cueGain: 1f);
        Assert.Equal(0.5f, buf[0]); // master L = 1 × 0.5
        Assert.Equal(1.0f, buf[1]); // master R = 2 × 0.5
        Assert.Equal(1.0f, buf[2]); // cue L = 1 × 1
        Assert.Equal(2.0f, buf[3]); // cue R = 2 × 1
    }

    [Fact]
    public void Cue_IsPreFader_FaderDownStillFullOnCue()
    {
        var buf = new float[4];
        // Channel fader all the way down (masterGain 0) but cued.
        _router.MixDeckInto(buf, Stereo(1f, 1f), frames: 1, channels: 4,
            masterGain: 0f, cueGain: 1f);
        Assert.Equal(0f, buf[0]); // silent to master
        Assert.Equal(0f, buf[1]);
        Assert.Equal(1f, buf[2]); // full on cue (pre-fader)
        Assert.Equal(1f, buf[3]);
    }

    [Fact]
    public void TwoChannel_WritesMasterOnly_NoCue()
    {
        var buf = new float[2]; // 1 frame, 2 channels — no ch3-4 exists
        _router.MixDeckInto(buf, Stereo(1f, 1f), frames: 1, channels: 2,
            masterGain: 1f, cueGain: 1f);
        Assert.Equal(1f, buf[0]);
        Assert.Equal(1f, buf[1]);
        // (no out-of-range write — buffer is length 2)
    }

    [Fact]
    public void Accumulates_TwoDecks_Sum()
    {
        var buf = new float[4];
        _router.MixDeckInto(buf, Stereo(1f, 1f), 1, 4, masterGain: 1f, cueGain: 0f);
        _router.MixDeckInto(buf, Stereo(2f, 2f), 1, 4, masterGain: 1f, cueGain: 1f);
        Assert.Equal(3f, buf[0]); // master = 1 + 2
        Assert.Equal(3f, buf[1]);
        Assert.Equal(2f, buf[2]); // cue = only deck 2
        Assert.Equal(2f, buf[3]);
    }

    // Vector paths (channels 2 and 4) and the scalar fallback against the old scalar
    // loop, bit for bit, at lengths that are not a multiple of any vector width,
    // with non-unity gains and a pre-filled buffer so the accumulate rounds.
    [Theory]
    [InlineData(2, 1)]
    [InlineData(2, 7)]
    [InlineData(2, 513)]
    [InlineData(4, 1)]
    [InlineData(4, 7)]
    [InlineData(4, 513)]
    [InlineData(3, 7)]
    [InlineData(6, 513)]
    public void MixDeckInto_matches_the_scalar_loop_bit_for_bit(int channels, int frames)
    {
        var rng = new Random(145 + channels * 1000 + frames);
        var stereo = new float[frames * 2];
        for (int i = 0; i < stereo.Length; i++) stereo[i] = Noise(rng);
        var actual = new float[frames * channels];
        for (int i = 0; i < actual.Length; i++) actual[i] = Noise(rng);
        var expected = (float[])actual.Clone();
        const float masterGain = 0.7071f, cueGain = 1.3f;

        _router.MixDeckInto(actual, stereo, frames, channels, masterGain, cueGain);

        for (int f = 0; f < frames; f++)
        {
            float l = stereo[f * 2];
            float r = stereo[f * 2 + 1];
            int o = f * channels;
            expected[o] += l * masterGain;
            expected[o + 1] += r * masterGain;
            if (channels >= 4)
            {
                expected[o + 2] += l * cueGain;
                expected[o + 3] += r * cueGain;
            }
        }
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.True(BitConverter.SingleToInt32Bits(expected[i]) == BitConverter.SingleToInt32Bits(actual[i]),
                $"sample {i}: expected {expected[i]:R}, actual {actual[i]:R}");
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public void MixDeckInto_allocates_nothing(int channels)
    {
        var stereo = new float[513 * 2];
        var buf = new float[513 * channels];
        _router.MixDeckInto(buf, stereo, 513, channels, 0.5f, 1f); // warm up (JIT)

        var result = new AllocationWindows(5).Measure(() =>
        {
            for (int i = 0; i < 3; i++) _router.MixDeckInto(buf, stereo, 513, channels, 0.5f, 1f);
        });

        Assert.True(result.AllocatingWindows <= 1, result.ToString());
    }

    // Values over several orders of magnitude, so the rounding of each add is exercised.
    private static float Noise(Random rng) =>
        (float)((rng.NextDouble() * 2 - 1) * Math.Pow(10, rng.Next(-4, 2)));
}
