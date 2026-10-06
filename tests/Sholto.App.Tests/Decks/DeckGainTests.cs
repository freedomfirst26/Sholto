using Sholto.App.Audio;
using Sholto.App.Decks;
using Xunit;

namespace Sholto.App.Tests;

/// <summary>A deck's channel fader x crossfade gain, applied to its mixer.</summary>
public class DeckGainTests
{
    private readonly IDeckPorts _ports = new TestDeckFactory().Create();
    private readonly DeckGain _gain;

    public DeckGainTests()
    {
        _gain = new DeckGain(_ports.Mixer);
    }

    [Fact]
    public void An_unmeasured_fader_plays_at_unity_but_reports_no_level()
    {
        Assert.Null(_gain.ChannelGain);
        Assert.False(_gain.GainKnown);
        Assert.Equal(1f, _ports.Mixer.Volume);
        Assert.Equal(0.0, _gain.EffectiveGain);
        Assert.False(_gain.IsMuted);
    }

    [Fact]
    public void The_channel_fader_combines_with_the_crossfade()
    {
        _gain.ChannelGain = 0.25;
        _gain.SetCrossfadeGain(0.5f);

        Assert.True(_gain.GainKnown);
        Assert.Equal(0.125f, _ports.Mixer.Volume);
        Assert.Equal(0.125, _gain.EffectiveGain, 6);
    }

    [Fact]
    public void A_measured_fader_at_zero_is_muted()
    {
        _gain.ChannelGain = 0;

        Assert.True(_gain.IsMuted);
        Assert.Equal(0f, _ports.Mixer.Volume);
    }

    [Fact]
    public void A_channel_gain_above_one_is_clamped_to_one()
    {
        _gain.ChannelGain = 1.7;

        Assert.Equal(1.0, _gain.ChannelGain);
        Assert.Equal(1f, _ports.Mixer.Volume);
    }

    [Fact]
    public void Setting_the_same_channel_gain_raises_nothing_the_second_time()
    {
        var changes = new List<DeckChange>();
        _gain.Changed += changes.Add;

        _gain.ChannelGain = 0.25;
        _gain.ChannelGain = 0.25;

        Assert.Equal([DeckChange.Volume, DeckChange.ChannelGain], changes);
    }
}
