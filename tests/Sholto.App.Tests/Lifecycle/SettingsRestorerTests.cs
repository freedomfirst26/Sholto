using Sholto.App.Lifecycle;
using Sholto.App.Audio;
using Sholto.App.Settings;
using Sholto.Data;

namespace Sholto.App.Tests;

/// <summary>The saved theme, waveform style and backspin settings restored at startup and saved on a later choice.</summary>
public class SettingsRestorerTests
{
    private static readonly Origin Answer = AppLifecycleRig.Origin;

    private static string NewMusicDir() => Directory.CreateTempSubdirectory("sholto-music-").FullName;

    /// <summary>The sequence runs on the thread pool, so a test waits for its effect.</summary>
    private static async Task Eventually(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("The condition never became true.");
            await Task.Delay(10);
        }
    }

    // ---- Theme ----------------------------------------------------------------------------------

    [Fact]
    public async Task The_saved_theme_is_announced_and_a_later_choice_is_saved()
    {
        var rig = new AppLifecycleRig(musicDirOverride: NewMusicDir());
        await rig.Settings.SetAsync(SettingsKeys.Theme, "Tokyo Night");

        rig.Lifecycle.Start();

        await Eventually(() => rig.ThemeFound.Received.Count == 1);
        Assert.Equal("Tokyo Night", rig.ThemeFound.Received[0].Name);
        await Eventually(() =>
        {
            rig.Lifecycle.Handle(new ChooseTheme("Serato", Answer));
            return rig.Settings.Peek(SettingsKeys.Theme) == "Serato";
        });
    }

    [Fact]
    public async Task Applying_the_restored_theme_is_not_saved_back()
    {
        var rig = new AppLifecycleRig(musicDirOverride: NewMusicDir());
        await rig.Settings.SetAsync(SettingsKeys.Theme, "Tokyo Night");
        // The interface sets the theme from the event, and its setter sends ChooseTheme.
        rig.Library.Bus.Subscribe(new ActionEventHandler<SavedThemeFound>(
            e => rig.Lifecycle.Handle(new ChooseTheme("Echo " + e.Name, Answer))));

        rig.Lifecycle.Start();

        await Eventually(() => rig.ThemeFound.Received.Count == 1);
        await Task.Delay(100);
        Assert.Equal("Tokyo Night", rig.Settings.Peek(SettingsKeys.Theme));
    }

    [Fact]
    public async Task Without_the_database_no_theme_is_restored()
    {
        var rig = new AppLifecycleRig(databaseAvailable: false);

        rig.Lifecycle.Start();

        await rig.Database.Opened;
        await Task.Delay(100);
        Assert.Empty(rig.ThemeFound.Received);
    }

    // ---- Waveform style -------------------------------------------------------------------------

    [Fact]
    public async Task The_saved_waveform_style_is_announced_and_a_later_choice_is_saved()
    {
        var rig = new AppLifecycleRig(musicDirOverride: NewMusicDir());
        await rig.Settings.SetAsync(SettingsKeys.WaveformStyle, "rgb");

        rig.Lifecycle.Start();

        await Eventually(() => rig.WaveformStyleFound.Received.Count == 1);
        Assert.Equal("rgb", rig.WaveformStyleFound.Received[0].Id);
        await Eventually(() =>
        {
            rig.Lifecycle.Handle(new ChooseWaveformStyle("three-band", Answer));
            return rig.Settings.Peek(SettingsKeys.WaveformStyle) == "three-band";
        });
    }

    [Fact]
    public async Task With_no_saved_waveform_style_nothing_is_announced_and_a_choice_is_still_saved()
    {
        var rig = new AppLifecycleRig(musicDirOverride: NewMusicDir());

        rig.Lifecycle.Start();

        await Eventually(() =>
        {
            rig.Lifecycle.Handle(new ChooseWaveformStyle("rgb", Answer));
            return rig.Settings.Peek(SettingsKeys.WaveformStyle) == "rgb";
        });
        Assert.Empty(rig.WaveformStyleFound.Received);
    }

    [Fact]
    public async Task Without_the_database_no_waveform_style_is_restored_or_saved()
    {
        var rig = new AppLifecycleRig(databaseAvailable: false);

        rig.Lifecycle.Start();

        await rig.Database.Opened;
        rig.Lifecycle.Handle(new ChooseWaveformStyle("rgb", Answer));
        await Task.Delay(100);
        Assert.Empty(rig.WaveformStyleFound.Received);
        Assert.Null(rig.Settings.Peek(SettingsKeys.WaveformStyle));
    }

    // ---- Backspin time and distance -------------------------------------------------------------

    // A choice made at any moment is saved, including one made before persistence arms, so a test sends each
    // value once and waits for it to land; there is nothing to retry.

    [Fact]
    public async Task The_saved_backspin_time_and_distance_are_restored_and_announced_and_later_choices_are_saved()
    {
        var rig = new AppLifecycleRig(musicDirOverride: NewMusicDir());
        await rig.Settings.SetAsync(SettingsKeys.BackspinTimeSeconds, "1.5");
        await rig.Settings.SetAsync(SettingsKeys.BackspinDistanceBeats, "6");

        rig.Lifecycle.Start();

        await Eventually(() => rig.BackspinFeel.Seconds == 1.5 && rig.BackspinFeel.Beats == 6);
        await Eventually(() => rig.BackspinTimeAnnounced.Received.Any(e => e.Seconds == 1.5));
        await Eventually(() => rig.BackspinDistanceAnnounced.Received.Any(e => e.Beats == 6));
        // Restoring is not a choice: nothing is written back.
        Assert.Equal("1.5", rig.Settings.Peek(SettingsKeys.BackspinTimeSeconds));
        Assert.Equal("6", rig.Settings.Peek(SettingsKeys.BackspinDistanceBeats));

        rig.BackspinFeel.Handle(new SetBackspinTime(2.25, Answer));
        await Eventually(() => rig.Settings.Peek(SettingsKeys.BackspinTimeSeconds) == "2.25");
        rig.BackspinFeel.Handle(new SetBackspinDistance(9, Answer));
        await Eventually(() => rig.Settings.Peek(SettingsKeys.BackspinDistanceBeats) == "9");
    }

    [Fact]
    public async Task With_nothing_saved_the_backspin_defaults_apply_and_a_choice_is_saved()
    {
        var rig = new AppLifecycleRig(musicDirOverride: NewMusicDir());
        Assert.Equal(0.6, rig.BackspinFeel.Seconds);
        Assert.Equal(2.0, rig.BackspinFeel.Beats);

        rig.Lifecycle.Start();

        rig.BackspinFeel.Handle(new SetBackspinTime(1.0, Answer));
        rig.BackspinFeel.Handle(new SetBackspinDistance(4, Answer));
        await Eventually(() => rig.Settings.Peek(SettingsKeys.BackspinTimeSeconds) == "1");
        await Eventually(() => rig.Settings.Peek(SettingsKeys.BackspinDistanceBeats) == "4");
    }

    [Fact]
    public async Task Backspin_values_survive_a_restart()
    {
        var first = new AppLifecycleRig(musicDirOverride: NewMusicDir());
        first.Lifecycle.Start();
        first.BackspinFeel.Handle(new SetBackspinTime(1.75, Answer));
        first.BackspinFeel.Handle(new SetBackspinDistance(12, Answer));
        await Eventually(() => first.Settings.Peek(SettingsKeys.BackspinTimeSeconds) is not null
                            && first.Settings.Peek(SettingsKeys.BackspinDistanceBeats) is not null);

        var second = new AppLifecycleRig(musicDirOverride: NewMusicDir());
        await second.Settings.SetAsync(SettingsKeys.BackspinTimeSeconds, first.Settings.Peek(SettingsKeys.BackspinTimeSeconds)!);
        await second.Settings.SetAsync(SettingsKeys.BackspinDistanceBeats, first.Settings.Peek(SettingsKeys.BackspinDistanceBeats)!);
        second.Lifecycle.Start();

        await Eventually(() => second.BackspinFeel.Seconds == 1.75 && second.BackspinFeel.Beats == 12);
    }

    [Fact]
    public async Task A_backspin_choice_made_before_the_database_opens_is_saved_once_it_does_and_beats_the_saved_value()
    {
        var rig = new AppLifecycleRig(gated: true, musicDirOverride: NewMusicDir());
        await rig.Settings.SetAsync(SettingsKeys.BackspinTimeSeconds, "0.9");
        await rig.Settings.SetAsync(SettingsKeys.BackspinDistanceBeats, "3");
        rig.Lifecycle.Start();

        rig.BackspinFeel.Handle(new SetBackspinTime(2.5, Answer));
        rig.BackspinFeel.Handle(new SetBackspinDistance(7, Answer));
        await Task.Delay(100);
        Assert.False(rig.Database.Opened.IsCompleted);
        Assert.Equal("0.9", rig.Settings.Peek(SettingsKeys.BackspinTimeSeconds));

        rig.Database.Release();

        await Eventually(() => rig.Settings.Peek(SettingsKeys.BackspinTimeSeconds) == "2.5");
        await Eventually(() => rig.Settings.Peek(SettingsKeys.BackspinDistanceBeats) == "7");
        // The saved values did not overwrite the user's choice.
        Assert.Equal(2.5, rig.BackspinFeel.Seconds);
        Assert.Equal(7.0, rig.BackspinFeel.Beats);
    }

    [Fact]
    public async Task Saved_backspin_values_outside_the_range_are_clamped_on_restore()
    {
        var rig = new AppLifecycleRig(musicDirOverride: NewMusicDir());
        await rig.Settings.SetAsync(SettingsKeys.BackspinTimeSeconds, "50");
        await rig.Settings.SetAsync(SettingsKeys.BackspinDistanceBeats, "-4");

        rig.Lifecycle.Start();

        await Eventually(() => rig.BackspinFeel.Seconds == SetBackspinTime.Max && rig.BackspinFeel.Beats == SetBackspinDistance.Min);
    }

    [Fact]
    public async Task The_old_backspin_release_key_is_ignored()
    {
        var rig = new AppLifecycleRig(musicDirOverride: NewMusicDir());
        await rig.Settings.SetAsync("backspin_release_multiplier", "4");

        rig.Lifecycle.Start();

        await Eventually(() => rig.Library.Library.Rows.Count == 3);
        await Task.Delay(200);
        Assert.Equal(SetBackspinTime.Default, rig.BackspinFeel.Seconds);
        Assert.Equal(SetBackspinDistance.Default, rig.BackspinFeel.Beats);
        Assert.Null(rig.Settings.Peek(SettingsKeys.BackspinTimeSeconds));
        Assert.Null(rig.Settings.Peek(SettingsKeys.BackspinDistanceBeats));
    }

    [Fact]
    public async Task Without_the_database_backspin_changes_apply_live_but_are_not_saved()
    {
        var rig = new AppLifecycleRig(databaseAvailable: false);

        rig.Lifecycle.Start();

        await rig.Database.Opened;
        rig.BackspinFeel.Handle(new SetBackspinTime(2, Answer));
        rig.BackspinFeel.Handle(new SetBackspinDistance(8, Answer));
        await Task.Delay(100);
        Assert.Equal(2.0, rig.BackspinFeel.Seconds);
        Assert.Equal(8.0, rig.BackspinFeel.Beats);
        Assert.Null(rig.Settings.Peek(SettingsKeys.BackspinTimeSeconds));
        Assert.Null(rig.Settings.Peek(SettingsKeys.BackspinDistanceBeats));
    }
}
