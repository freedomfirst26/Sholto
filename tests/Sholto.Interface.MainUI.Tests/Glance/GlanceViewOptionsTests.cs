using Microsoft.Extensions.Options;
using Sholto.Interface.MainUI.Controls.WaveformStyles;
using Sholto.Interface.MainUI.ViewModels.Glance;

namespace Sholto.Interface.MainUI.Tests.Glance;

/// <summary>The view option classes keep the values they had before they became options.</summary>
public class GlanceViewOptionsTests
{
    [Fact]
    public void Glance_view_defaults_equal_the_former_constants()
    {
        var options = new GlanceViewOptions();

        Assert.Equal(10, options.TagRailLimit);
        Assert.Equal(150, options.IndicatorDelayMs);
        Assert.Equal(300, options.IndicatorMinimumMs);
        Assert.Equal(2000, options.ClearArmMs);
        Assert.Equal(45, options.LowTimeSeconds);
        Assert.Equal(120, options.FallbackBpm);
    }

    [Fact]
    public void Deck_view_defaults_equal_the_former_constants() =>
        Assert.Equal(0.3, new DeckViewOptions().StemChipMinOpacity);

    [Fact]
    public void Waveform_style_defaults_equal_the_former_constants()
    {
        var options = new WaveformStyleOptions();

        Assert.Equal(0.75f, options.RgbFloorShare);
        Assert.Equal(8, options.RgbColourRadius);
    }

    [Fact]
    public void A_longer_low_time_makes_a_slot_low_earlier()
    {
        var decks = new FakeDeckClockSource();
        var reading = new DeckClockReading(true, true, "A", 180, 100, 1.0, "8A", null, 128);
        decks.Set(0, reading);
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var byDefault = new DeckSlotFactory(decks, new FixedMotionPreference(false), Options.Create(new GlanceViewOptions())).Create(0);
        var longer = new DeckSlotFactory(decks, new FixedMotionPreference(false),
            Options.Create(new GlanceViewOptions { LowTimeSeconds = 90 })).Create(0);

        byDefault.Update(isTarget: false, isReference: false, replacePending: false, now);
        longer.Update(isTarget: false, isReference: false, replacePending: false, now);

        // 80 s left: not low under the default 45 s, low under 90 s.
        Assert.False(byDefault.IsLow);
        Assert.True(longer.IsLow);
    }
}
