using Sholto.App.Audio;
using Sholto.App.Settings;
using Sholto.Data;

namespace Sholto.App.Tests;

/// <summary>The startup sequence on its own, with no window and no Avalonia: which questions the App asks
/// and when, what it persists, and what it does when the library database is there or is not. The numbered
/// properties are the ones the old App.InitializeServices kept (P1-P8, task C38).</summary>
public class AppLifecycleTests
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

    // ---- Music folder ---------------------------------------------------------------------------

    [Fact]
    public async Task With_the_database_and_a_reachable_saved_folder_the_library_is_scanned_with_its_stores_and_nothing_is_asked()
    {
        var rig = new AppLifecycleRig();
        await rig.Settings.SetAsync(SettingsKeys.MusicDir, NewMusicDir());

        rig.Lifecycle.Start();

        await Eventually(() => rig.Library.Library.Rows.Count == 3);
        Assert.Empty(rig.MusicFolderAsked.Received);
        Assert.All(rig.Library.Library.Rows, r => Assert.NotEqual(Guid.Empty, r.TrackId));
        Assert.False(rig.Library.Library.IsUnreachable);
    }

    [Fact]
    public async Task The_environment_override_is_scanned_without_asking_even_when_nothing_is_saved()
    {
        var rig = new AppLifecycleRig(musicDirOverride: NewMusicDir());

        rig.Lifecycle.Start();

        await Eventually(() => rig.Library.Library.Rows.Count == 3);
        Assert.Empty(rig.MusicFolderAsked.Received);
    }

    [Fact]
    public async Task First_run_asks_for_a_folder_then_saves_it_and_scans()
    {
        var rig = new AppLifecycleRig();
        var chosen = NewMusicDir();
        rig.Library.Bus.Subscribe(new ActionEventHandler<MusicFolderNeeded>(
            _ => rig.Lifecycle.Handle(new ChooseMusicFolder(chosen, Answer))));

        rig.Lifecycle.Start();

        await Eventually(() => rig.Library.Library.Rows.Count == 3);
        var asked = Assert.Single(rig.MusicFolderAsked.Received);
        Assert.Equal(MusicFolderReason.FirstRun, asked.Reason);
        Assert.Null(asked.MissingPath);
        Assert.Equal(chosen, rig.Settings.Peek(SettingsKeys.MusicDir));
    }

    [Fact]
    public async Task A_cancelled_first_run_picker_saves_nothing_and_scans_nothing()
    {
        var rig = new AppLifecycleRig();
        rig.Library.Bus.Subscribe(new ActionEventHandler<MusicFolderNeeded>(
            _ => rig.Lifecycle.Handle(new ChooseMusicFolder(null, Answer))));

        rig.Lifecycle.Start();

        await Eventually(() => rig.MusicFolderAsked.Received.Count == 1);
        await rig.Database.Opened;
        await Task.Delay(100);
        Assert.Empty(rig.Library.Library.Rows);
        Assert.Null(rig.Settings.Peek(SettingsKeys.MusicDir));
    }

    [Fact]
    public async Task An_unreachable_saved_folder_shows_the_banner_and_asks_again_naming_the_missing_path()
    {
        var rig = new AppLifecycleRig();
        const string gone = "/media/definitely/not/mounted";
        await rig.Settings.SetAsync(SettingsKeys.MusicDir, gone);

        rig.Lifecycle.Start();

        await Eventually(() => rig.MusicFolderAsked.Received.Count == 1);
        var asked = rig.MusicFolderAsked.Received[0];
        Assert.Equal(MusicFolderReason.DriveNotFound, asked.Reason);
        Assert.Equal(gone, asked.MissingPath);
        Assert.Equal(gone, rig.Library.Library.UnreachablePath);
        Assert.Empty(rig.Library.Library.Rows);
    }

    [Fact]
    public async Task Cancelling_the_picker_for_an_unreachable_folder_keeps_the_saved_path_for_next_launch()
    {
        var rig = new AppLifecycleRig();
        const string gone = "/media/definitely/not/mounted";
        await rig.Settings.SetAsync(SettingsKeys.MusicDir, gone);
        rig.Library.Bus.Subscribe(new ActionEventHandler<MusicFolderNeeded>(
            _ => rig.Lifecycle.Handle(new ChooseMusicFolder(null, Answer))));

        rig.Lifecycle.Start();

        await Eventually(() => rig.MusicFolderAsked.Received.Count == 1);
        await Task.Delay(100);
        Assert.Equal(gone, rig.Settings.Peek(SettingsKeys.MusicDir));
        Assert.Empty(rig.Library.Library.Rows);
        Assert.True(rig.Library.Library.IsUnreachable);
    }

    [Fact]
    public async Task Choosing_a_folder_for_an_unreachable_one_replaces_the_saved_path_and_clears_the_banner()
    {
        var rig = new AppLifecycleRig();
        await rig.Settings.SetAsync(SettingsKeys.MusicDir, "/media/definitely/not/mounted");
        var chosen = NewMusicDir();
        rig.Library.Bus.Subscribe(new ActionEventHandler<MusicFolderNeeded>(
            _ => rig.Lifecycle.Handle(new ChooseMusicFolder(chosen, Answer))));

        rig.Lifecycle.Start();

        await Eventually(() => rig.Library.Library.Rows.Count == 3);
        Assert.Equal(chosen, rig.Settings.Peek(SettingsKeys.MusicDir));
        Assert.False(rig.Library.Library.IsUnreachable);
    }

    [Fact]
    public async Task Without_the_database_nothing_is_read_or_saved_and_the_scan_runs_with_no_stores()
    {
        var rig = new AppLifecycleRig(databaseAvailable: false);
        var chosen = NewMusicDir();
        rig.Library.Bus.Subscribe(new ActionEventHandler<MusicFolderNeeded>(
            _ => rig.Lifecycle.Handle(new ChooseMusicFolder(chosen, Answer))));

        rig.Lifecycle.Start();

        // No saved folder can be read, so it is a first run every launch.
        await Eventually(() => rig.Library.Library.Rows.Count == 3);
        Assert.Equal(MusicFolderReason.FirstRun, Assert.Single(rig.MusicFolderAsked.Received).Reason);
        Assert.Null(await rig.MusicDirPreference.GetAsync());
        Assert.All(rig.Library.Library.Rows, r => Assert.Equal(Guid.Empty, r.TrackId));
    }

    [Fact]
    public async Task The_menu_asks_for_a_new_folder_and_a_cancel_changes_nothing()
    {
        var rig = new AppLifecycleRig();
        var saved = NewMusicDir();
        await rig.Settings.SetAsync(SettingsKeys.MusicDir, saved);
        rig.Lifecycle.Start();
        await Eventually(() => rig.Library.Library.Rows.Count == 3);
        rig.Library.Bus.Subscribe(new ActionEventHandler<MusicFolderNeeded>(
            _ => rig.Lifecycle.Handle(new ChooseMusicFolder(null, Answer))));

        rig.Lifecycle.Handle(new ChangeMusicFolder(Answer));

        await Eventually(() => rig.MusicFolderAsked.Received.Count == 1);
        await Task.Delay(100);
        Assert.Equal(MusicFolderReason.Change, rig.MusicFolderAsked.Received[0].Reason);
        Assert.Equal(saved, rig.Settings.Peek(SettingsKeys.MusicDir));
        Assert.Equal(3, rig.Library.Library.Rows.Count);
    }

    [Fact]
    public async Task The_menu_picks_a_new_folder_saves_it_and_rescans_even_when_it_is_the_same_one()
    {
        var rig = new AppLifecycleRig();
        var saved = NewMusicDir();
        await rig.Settings.SetAsync(SettingsKeys.MusicDir, saved);
        rig.Lifecycle.Start();
        await Eventually(() => rig.Library.Library.Rows.Count == 3);
        var rescans = 0;
        rig.Library.Library.RowsChanged += _ => rescans++;
        rig.Library.Bus.Subscribe(new ActionEventHandler<MusicFolderNeeded>(
            _ => rig.Lifecycle.Handle(new ChooseMusicFolder(saved, Answer))));

        rig.Lifecycle.Handle(new ChangeMusicFolder(Answer));

        await Eventually(() => rescans >= 1);
        Assert.Equal(saved, rig.Settings.Peek(SettingsKeys.MusicDir));
    }

    // ---- Output device --------------------------------------------------------------------------

    private static readonly AudioDevice Speakers = new("Speakers", IsDefault: true);
    private static readonly AudioDevice Headphones = new("Headphones", IsDefault: false);
    private static readonly AudioDevice ControllerCard = new(AppLifecycleRig.ControllerCard, IsDefault: false);

    [Fact]
    public async Task A_saved_output_device_that_is_still_there_starts_audio_without_asking()
    {
        var rig = new AppLifecycleRig(devices: [Speakers, Headphones, ControllerCard], musicDirOverride: NewMusicDir());
        await rig.Settings.SetAsync(SettingsKeys.OutputDevice, "Headphones");

        rig.Lifecycle.Start();

        await Eventually(() => rig.Audio.Started);
        Assert.Equal("Headphones", rig.Audio.StartedOn);
        Assert.Empty(rig.DeviceAsked.Received);
    }

    [Fact]
    public async Task With_no_saved_device_the_picker_lists_everything_but_the_controllers_card_and_the_choice_is_saved()
    {
        var rig = new AppLifecycleRig(devices: [Speakers, Headphones, ControllerCard], musicDirOverride: NewMusicDir());
        rig.Library.Bus.Subscribe(new ActionEventHandler<OutputDeviceNeeded>(
            _ => rig.Lifecycle.Handle(new ChooseOutputDevice("Speakers", Answer))));

        rig.Lifecycle.Start();

        await Eventually(() => rig.Audio.Started);
        var asked = Assert.Single(rig.DeviceAsked.Received);
        Assert.Equal(new[] { "Speakers", "Headphones" }, asked.Devices.Select(d => d.Name));
        Assert.Null(asked.CurrentName);
        Assert.Equal("Speakers", rig.Audio.StartedOn);
        Assert.Equal("Speakers", rig.Settings.Peek(SettingsKeys.OutputDevice));
    }

    [Fact]
    public async Task A_cancelled_device_picker_falls_back_on_the_controllers_card_when_it_is_connected()
    {
        var rig = new AppLifecycleRig(devices: [Speakers, ControllerCard], musicDirOverride: NewMusicDir());
        rig.Library.Bus.Subscribe(new ActionEventHandler<OutputDeviceNeeded>(
            _ => rig.Lifecycle.Handle(new ChooseOutputDevice(null, Answer))));

        rig.Lifecycle.Start();

        await Eventually(() => rig.Audio.Started);
        Assert.Null(rig.Audio.StartedOn);
        Assert.Null(rig.Settings.Peek(SettingsKeys.OutputDevice));
    }

    [Fact]
    public async Task A_cancelled_device_picker_with_no_controller_card_starts_no_audio()
    {
        var rig = new AppLifecycleRig(devices: [Speakers], musicDirOverride: NewMusicDir());
        rig.Library.Bus.Subscribe(new ActionEventHandler<OutputDeviceNeeded>(
            _ => rig.Lifecycle.Handle(new ChooseOutputDevice(null, Answer))));

        rig.Lifecycle.Start();

        await Eventually(() => rig.DeviceAsked.Received.Count == 1);
        await Task.Delay(100);
        Assert.False(rig.Audio.Started);
    }

    [Fact]
    public async Task With_no_output_devices_at_all_nothing_is_asked_and_no_audio_starts()
    {
        var rig = new AppLifecycleRig(devices: [], musicDirOverride: NewMusicDir());

        rig.Lifecycle.Start();

        await Eventually(() => rig.Enumerator.Calls == 1);
        await Task.Delay(100);
        Assert.Empty(rig.DeviceAsked.Received);
        Assert.False(rig.Audio.Started);
    }

    [Fact]
    public async Task The_output_devices_are_listed_before_the_database_is_awaited()
    {
        var rig = new AppLifecycleRig(gated: true, devices: [Speakers], musicDirOverride: NewMusicDir());

        rig.Lifecycle.Start();

        await Eventually(() => rig.Enumerator.Calls == 1);
        await Task.Delay(100);
        Assert.False(rig.Database.Opened.IsCompleted);
        Assert.Empty(rig.DeviceAsked.Received);

        rig.Database.Release();
        await Eventually(() => rig.DeviceAsked.Received.Count == 1);
    }

    [Fact]
    public async Task Without_the_database_the_device_choice_is_not_saved()
    {
        var rig = new AppLifecycleRig(databaseAvailable: false, devices: [Speakers], musicDirOverride: NewMusicDir());
        rig.Library.Bus.Subscribe(new ActionEventHandler<OutputDeviceNeeded>(
            _ => rig.Lifecycle.Handle(new ChooseOutputDevice("Speakers", Answer))));

        rig.Lifecycle.Start();

        await Eventually(() => rig.Audio.Started);
        Assert.Equal("Speakers", rig.Audio.StartedOn);
        Assert.Null(await rig.OutputDevicePreference.GetAsync());
    }

    [Fact]
    public async Task The_menu_switches_to_another_device_but_not_to_the_one_already_in_use()
    {
        var rig = new AppLifecycleRig(devices: [Speakers, Headphones, ControllerCard], musicDirOverride: NewMusicDir());
        await rig.Settings.SetAsync(SettingsKeys.OutputDevice, "Speakers");
        var reply = "Speakers";
        rig.Library.Bus.Subscribe(new ActionEventHandler<OutputDeviceNeeded>(
            _ => rig.Lifecycle.Handle(new ChooseOutputDevice(reply, Answer))));
        // The preference reads wait for the database, which opens when the lifecycle starts.
        rig.Lifecycle.Start();
        await Eventually(() => rig.Audio.Started);
        rig.DeviceAsked.Received.Clear();

        rig.Lifecycle.Handle(new ChangeOutputDevice(Answer));
        await Eventually(() => rig.DeviceAsked.Received.Count == 1);
        await Task.Delay(100);
        Assert.Equal("Speakers", rig.DeviceAsked.Received[0].CurrentName);
        Assert.Null(rig.Audio.SwitchedTo);

        reply = "Headphones";
        rig.Lifecycle.Handle(new ChangeOutputDevice(Answer));

        await Eventually(() => rig.Audio.SwitchedTo is not null);
        Assert.Equal("Headphones", rig.Audio.SwitchedTo);
        Assert.Equal("Headphones", rig.Settings.Peek(SettingsKeys.OutputDevice));
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

    // ---- Database services ----------------------------------------------------------------------

    [Fact]
    public async Task With_the_database_the_tag_and_crate_services_markers_and_multiplier_store_are_attached()
    {
        var rig = new AppLifecycleRig(musicDirOverride: NewMusicDir());
        var attached = 0;
        rig.Library.Library.ServicesAttached += (_, _) => attached++;

        rig.Lifecycle.Start();
        await Eventually(() => rig.Library.Library.Rows.Count == 3);

        Assert.Equal(1, attached);
        var alpha = LibrarySessionRig.Alpha;
        rig.Library.Deck1.LoadTrack(alpha, alpha.FilePath, [], bpmMultiplier: 1.0);
        rig.Library.Deck1.HalveBpm();
        Assert.Equal(new[] { (alpha.FilePath, 0.5) }, rig.Library.Multipliers.Puts);
        var marked = new List<(int Deck, double Secs)>();
        rig.DeckMarkers.MarkerAdded += (deck, secs) => marked.Add((deck, secs));
        await rig.DeckMarkers.AddAsync(0);
        Assert.Single(marked);
    }

    [Fact]
    public async Task Without_the_database_none_of_that_is_attached()
    {
        var rig = new AppLifecycleRig(databaseAvailable: false, musicDirOverride: NewMusicDir());
        var attached = 0;
        rig.Library.Library.ServicesAttached += (_, _) => attached++;

        rig.Lifecycle.Start();
        await rig.Database.Opened;
        await Eventually(() => rig.Library.Library.Rows.Count == 3);

        Assert.Equal(0, attached);
        var alpha = LibrarySessionRig.Alpha;
        rig.Library.Deck1.LoadTrack(alpha, alpha.FilePath, [], bpmMultiplier: 1.0);
        rig.Library.Deck1.HalveBpm();
        Assert.Empty(rig.Library.Multipliers.Puts);
        Assert.Equal(0.5, rig.Library.Library.GetBpmMultiplierFor(alpha.FilePath));
        var marked = 0;
        rig.DeckMarkers.MarkerAdded += (_, _) => marked++;
        await rig.DeckMarkers.AddAsync(0);
        Assert.Equal(0, marked);
    }

    [Fact]
    public void Stopping_stops_the_audio_engine()
    {
        var rig = new AppLifecycleRig();

        rig.Lifecycle.Stop();

        Assert.True(rig.Audio.Stopped);
    }
}
