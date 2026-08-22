using Xunit;

namespace Sholto.Audio.Tests;

public class CueOutputRouterTests
{
    // One stereo frame [L=1, R=2] for brevity.
    private static float[] Stereo(float l, float r) => [l, r];

    [Fact]
    public void FourChannel_MasterOnCh12_CueOnCh34()
    {
        var buf = new float[4]; // 1 frame, 4 channels
        CueOutputRouter.MixDeckInto(buf, Stereo(1f, 2f), frames: 1, channels: 4,
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
        CueOutputRouter.MixDeckInto(buf, Stereo(1f, 1f), frames: 1, channels: 4,
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
        CueOutputRouter.MixDeckInto(buf, Stereo(1f, 1f), frames: 1, channels: 2,
            masterGain: 1f, cueGain: 1f);
        Assert.Equal(1f, buf[0]);
        Assert.Equal(1f, buf[1]);
        // (no out-of-range write — buffer is length 2)
    }

    [Fact]
    public void Accumulates_TwoDecks_Sum()
    {
        var buf = new float[4];
        CueOutputRouter.MixDeckInto(buf, Stereo(1f, 1f), 1, 4, masterGain: 1f, cueGain: 0f);
        CueOutputRouter.MixDeckInto(buf, Stereo(2f, 2f), 1, 4, masterGain: 1f, cueGain: 1f);
        Assert.Equal(3f, buf[0]); // master = 1 + 2
        Assert.Equal(3f, buf[1]);
        Assert.Equal(2f, buf[2]); // cue = only deck 2
        Assert.Equal(2f, buf[3]);
    }
}
