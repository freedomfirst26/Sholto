using Sholto.App.Library;
using Sholto.App.Glance;
using Sholto.Data;

namespace Sholto.App.Tests.Glance;

/// <summary>The default load target: an empty deck first, else the one not playing, else the one with less
/// real time left.</summary>
public class SuggestLoadTargetHandlerTests
{
    private static readonly Track One = new("/music/one.mp3", "One", "A", TimeSpan.FromMinutes(5));
    private static readonly Track Two = new("/music/two.mp3", "Two", "B", TimeSpan.FromMinutes(5));

    private static int Suggest(GlanceHandlerRig rig) =>
        new SuggestLoadTargetHandler(rig.Decks).Handle(new SuggestLoadTarget());

    [Fact]
    public void Both_decks_empty_suggests_deck_1()
    {
        Assert.Equal(0, Suggest(new GlanceHandlerRig()));
    }

    [Fact]
    public void A_playing_deck_1_with_deck_2_empty_suggests_deck_2()
    {
        var rig = new GlanceHandlerRig();
        rig.Load(0, One, playing: true, positionSec: 10);

        Assert.Equal(1, Suggest(rig));
    }

    [Fact]
    public void Only_deck_1_loaded_and_paused_suggests_the_empty_deck_2()
    {
        var rig = new GlanceHandlerRig();
        rig.Load(0, One);

        Assert.Equal(1, Suggest(rig));
    }

    [Fact]
    public void Only_deck_2_loaded_suggests_the_empty_deck_1()
    {
        var rig = new GlanceHandlerRig();
        rig.Load(1, Two, playing: true);

        Assert.Equal(0, Suggest(rig));
    }

    [Fact]
    public void A_playing_deck_1_with_deck_2_loaded_and_paused_suggests_deck_2()
    {
        var rig = new GlanceHandlerRig();
        rig.Load(0, One, playing: true, positionSec: 10);
        rig.Load(1, Two);

        Assert.Equal(1, Suggest(rig));
    }

    [Fact]
    public void A_playing_deck_2_with_deck_1_loaded_and_paused_suggests_deck_1()
    {
        var rig = new GlanceHandlerRig();
        rig.Load(0, One);
        rig.Load(1, Two, playing: true, positionSec: 10);

        Assert.Equal(0, Suggest(rig));
    }

    [Fact]
    public void Both_loaded_and_paused_suggests_deck_1()
    {
        var rig = new GlanceHandlerRig();
        rig.Load(0, One);
        rig.Load(1, Two);

        Assert.Equal(0, Suggest(rig));
    }

    [Fact]
    public void Both_playing_suggests_the_deck_with_less_time_left()
    {
        var rig = new GlanceHandlerRig();
        rig.Load(0, One, playing: true, positionSec: 260);   // 0:40 left
        rig.Load(1, Two, playing: true, positionSec: 50);    // 4:10 left

        Assert.Equal(0, Suggest(rig));

        rig.Load(0, One, playing: true, positionSec: 50);
        rig.Load(1, Two, playing: true, positionSec: 260);

        Assert.Equal(1, Suggest(rig));
    }

    [Fact]
    public void Time_left_is_measured_in_real_time_so_playback_speed_counts()
    {
        var rig = new GlanceHandlerRig();
        // Deck 1: 200 s of track left at 1.25x is 160 s real. Deck 2: 170 s left at 1x is 170 s real.
        // By track time deck 2 has less left; by real time deck 1 has.
        rig.Load(0, One, playing: true, positionSec: 100, speed: 1.25);
        rig.Load(1, Two, playing: true, positionSec: 130);

        Assert.Equal(0, Suggest(rig));
    }

    [Fact]
    public void Equal_time_left_suggests_deck_1()
    {
        var rig = new GlanceHandlerRig();
        rig.Load(0, One, playing: true, positionSec: 100);
        rig.Load(1, Two, playing: true, positionSec: 100);

        Assert.Equal(0, Suggest(rig));
    }
}
