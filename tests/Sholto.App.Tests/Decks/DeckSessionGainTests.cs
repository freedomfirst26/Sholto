using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis.Analyzers.Waveform;
using Sholto.App.Library;
using Sholto.Data;
using Sholto.App.Decks;
using Xunit;

namespace Sholto.App.Tests;

/// <summary>Fader pickup and channel gain on the deck session: an unmeasured fader plays at unity.</summary>
public class DeckSessionGainTests
{
    [Fact]
    public void An_unmeasured_fader_plays_at_unity_but_reports_no_level()
    {
        var rig = new DeckSessionRig(new TestDeckFactory().Create());

        Assert.Null(rig.Session.ChannelGain);
        Assert.False(rig.Session.GainKnown);
        Assert.Equal(1f, rig.Ports.Mixer.Volume);       // audible: unity x crossfade 1
        Assert.Equal(0.0, rig.Session.EffectiveGain);   // the UI draws no level until measured
        Assert.False(rig.Session.IsMuted);              // unknown is not muted
    }

    [Fact]
    public void The_first_fader_move_adopts_the_real_position_and_it_combines_with_the_crossfade()
    {
        var rig = new DeckSessionRig(new TestDeckFactory().Create());

        rig.Session.ChannelGain = 0.25;
        Assert.True(rig.Session.GainKnown);
        Assert.Equal(0.25f, rig.Ports.Mixer.Volume);

        rig.Session.SetCrossfadeGain(0.5f);
        Assert.Equal(0.125f, rig.Ports.Mixer.Volume);
        Assert.Equal(0.125, rig.Session.EffectiveGain, 6);
    }

    [Fact]
    public void A_measured_fader_at_zero_is_muted()
    {
        var rig = new DeckSessionRig(new TestDeckFactory().Create());

        rig.Session.ChannelGain = 0;

        Assert.True(rig.Session.IsMuted);
        Assert.Equal(0f, rig.Ports.Mixer.Volume);
    }

    [Fact]
    public void A_channel_gain_above_one_is_clamped_to_one()
    {
        var rig = new DeckSessionRig(new TestDeckFactory().Create());

        rig.Session.ChannelGain = 1.7;

        Assert.Equal(1.0, rig.Session.ChannelGain);
        Assert.Equal(1f, rig.Ports.Mixer.Volume);
    }

    [Fact]
    public void Setting_the_same_channel_gain_raises_nothing()
    {
        var rig = new DeckSessionRig(new TestDeckFactory().Create());
        var changes = new List<DeckChange>();
        rig.Session.Changed += changes.Add;

        rig.Session.ChannelGain = 0.25;
        rig.Session.ChannelGain = 0.25;

        Assert.Single(changes, DeckChange.ChannelGain);
    }
}
