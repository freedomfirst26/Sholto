using Sholto.App.Lifecycle;
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

    // ---- Tool check -----------------------------------------------------------------------------

    [Fact]
    public async Task Starting_announces_the_boot_time_tool_check()
    {
        var check = new SystemCheck([new ToolPresence(ExternalToolNames.Madmom, ToolCapabilities.Beats, true, null)]);
        var rig = new AppLifecycleRig(systemCheck: check);
        var reported = new RecordingHandler<SystemCheckReported>();
        rig.Library.Bus.Subscribe(reported);

        await rig.OnAppThreadAsync(rig.Lifecycle.Start);

        var announced = Assert.Single(reported.Received);
        Assert.Equal(SystemHealth.Offline, announced.Health);
        Assert.Equal(check.ToReported().Tools, announced.Tools);
    }

    // ---- Folders --------------------------------------------------------------------------------

    private static string NewAnalysisDir() => Directory.CreateTempSubdirectory("sholto-analysis-").FullName;

    /// <summary>The "interface" answering every <see cref="FoldersNeeded"/> with this pick (both null cancels).</summary>
    private static void AnswerFolders(AppLifecycleRig rig, string? music, string? analysis) =>
        rig.Library.Bus.Subscribe(new ActionEventHandler<FoldersNeeded>(
            _ => rig.Folders.Handle(new ChooseFolders(music, analysis, Answer))));

    [Fact]
    public async Task With_the_database_and_reachable_saved_folders_the_library_is_scanned_with_its_stores_and_nothing_is_asked()
    {
        var rig = new AppLifecycleRig();
        await rig.Settings.SetAsync(SettingsKeys.MusicDir, NewMusicDir());
        await rig.Settings.SetAsync(SettingsKeys.AnalysisDir, NewAnalysisDir());

        rig.Lifecycle.Start();

        await Eventually(() => rig.Library.Library.Rows.Count == 3);
        Assert.Empty(rig.FoldersAsked.Received);
        Assert.All(rig.Library.Library.Rows, r => Assert.NotEqual(Guid.Empty, r.TrackId));
        Assert.False(rig.Library.Library.IsUnreachable);
    }

    [Fact]
    public async Task The_environment_overrides_are_used_without_asking_even_when_nothing_is_saved()
    {
        var rig = new AppLifecycleRig(musicDirOverride: NewMusicDir());

        rig.Lifecycle.Start();

        await Eventually(() => rig.Library.Library.Rows.Count == 3);
        Assert.Empty(rig.FoldersAsked.Received);
    }

    [Fact]
    public async Task First_run_asks_for_both_folders_then_saves_them_and_scans()
    {
        var rig = new AppLifecycleRig();
        var chosen = NewMusicDir();
        var analysis = NewAnalysisDir();
        AnswerFolders(rig, chosen, analysis);

        rig.Lifecycle.Start();

        await Eventually(() => rig.Library.Library.Rows.Count == 3);
        var asked = Assert.Single(rig.FoldersAsked.Received);
        Assert.Equal(FoldersReason.FirstRun, asked.Reason);
        Assert.Null(asked.Music.Path);
        Assert.Equal(chosen, rig.Settings.Peek(SettingsKeys.MusicDir));
        Assert.Equal(analysis, rig.Settings.Peek(SettingsKeys.AnalysisDir));
    }

    [Fact]
    public async Task A_cancelled_first_run_question_saves_nothing_and_scans_nothing()
    {
        var rig = new AppLifecycleRig();
        AnswerFolders(rig, null, null);

        rig.Lifecycle.Start();

        await Eventually(() => rig.FoldersAsked.Received.Count == 1);
        await rig.Database.Opened;
        await Task.Delay(100);
        Assert.Empty(rig.Library.Library.Rows);
        Assert.Null(rig.Settings.Peek(SettingsKeys.MusicDir));
        Assert.Null(rig.Settings.Peek(SettingsKeys.AnalysisDir));
    }

    [Fact]
    public async Task An_unreachable_saved_music_folder_shows_the_banner_and_asks_again_naming_the_missing_path()
    {
        var rig = new AppLifecycleRig();
        const string gone = "/media/definitely/not/mounted";
        await rig.Settings.SetAsync(SettingsKeys.MusicDir, gone);
        await rig.Settings.SetAsync(SettingsKeys.AnalysisDir, NewAnalysisDir());

        rig.Lifecycle.Start();

        await Eventually(() => rig.FoldersAsked.Received.Count == 1);
        var asked = rig.FoldersAsked.Received[0];
        Assert.Equal(FoldersReason.DriveNotFound, asked.Reason);
        Assert.Equal(gone, asked.Music.Path);
        Assert.Equal(FolderRowState.Missing, asked.Music.State);
        Assert.Equal(gone, rig.Library.Library.UnreachablePath);
        Assert.Empty(rig.Library.Library.Rows);
    }

    [Fact]
    public async Task Cancelling_the_question_for_an_unreachable_folder_keeps_the_saved_path_for_next_launch()
    {
        var rig = new AppLifecycleRig();
        const string gone = "/media/definitely/not/mounted";
        await rig.Settings.SetAsync(SettingsKeys.MusicDir, gone);
        await rig.Settings.SetAsync(SettingsKeys.AnalysisDir, NewAnalysisDir());
        AnswerFolders(rig, null, null);

        rig.Lifecycle.Start();

        await Eventually(() => rig.FoldersAsked.Received.Count == 1);
        await Task.Delay(100);
        Assert.Equal(gone, rig.Settings.Peek(SettingsKeys.MusicDir));
        Assert.Empty(rig.Library.Library.Rows);
        Assert.True(rig.Library.Library.IsUnreachable);
    }

    [Fact]
    public async Task Choosing_a_folder_for_an_unreachable_one_replaces_the_saved_path_and_clears_the_banner()
    {
        var rig = new AppLifecycleRig();
        var analysis = NewAnalysisDir();
        await rig.Settings.SetAsync(SettingsKeys.MusicDir, "/media/definitely/not/mounted");
        await rig.Settings.SetAsync(SettingsKeys.AnalysisDir, analysis);
        var chosen = NewMusicDir();
        AnswerFolders(rig, chosen, analysis);

        rig.Lifecycle.Start();

        await Eventually(() => rig.Library.Library.Rows.Count == 3);
        Assert.Equal(chosen, rig.Settings.Peek(SettingsKeys.MusicDir));
        Assert.False(rig.Library.Library.IsUnreachable);
    }

    [Fact]
    public async Task Without_the_database_nothing_is_read_or_saved_and_the_scan_runs_with_no_stores()
    {
        var rig = new AppLifecycleRig(databaseAvailable: false);
        AnswerFolders(rig, NewMusicDir(), NewAnalysisDir());

        rig.Lifecycle.Start();

        // No saved folder can be read, so it is a first run every launch.
        await Eventually(() => rig.Library.Library.Rows.Count == 3);
        Assert.Equal(FoldersReason.FirstRun, Assert.Single(rig.FoldersAsked.Received).Reason);
        Assert.Null(await rig.MusicDirPreference.GetAsync());
        Assert.All(rig.Library.Library.Rows, r => Assert.Equal(Guid.Empty, r.TrackId));
    }

    [Fact]
    public async Task The_menu_asks_about_the_folders_and_a_cancel_changes_nothing()
    {
        var rig = new AppLifecycleRig();
        var saved = NewMusicDir();
        var analysis = NewAnalysisDir();
        await rig.Settings.SetAsync(SettingsKeys.MusicDir, saved);
        await rig.Settings.SetAsync(SettingsKeys.AnalysisDir, analysis);
        rig.Lifecycle.Start();
        await Eventually(() => rig.Library.Library.Rows.Count == 3);
        AnswerFolders(rig, null, null);

        rig.Folders.Handle(new ChangeFolders(Answer));

        await Eventually(() => rig.FoldersAsked.Received.Count == 1);
        await Task.Delay(100);
        Assert.Equal(FoldersReason.Change, rig.FoldersAsked.Received[0].Reason);
        Assert.Equal(saved, rig.Settings.Peek(SettingsKeys.MusicDir));
        Assert.Equal(analysis, rig.Settings.Peek(SettingsKeys.AnalysisDir));
        Assert.Equal(3, rig.Library.Library.Rows.Count);
    }

    [Fact]
    public async Task The_menu_picks_new_folders_saves_them_and_rescans_even_when_the_music_folder_is_the_same_one()
    {
        var rig = new AppLifecycleRig();
        var saved = NewMusicDir();
        var analysis = NewAnalysisDir();
        await rig.Settings.SetAsync(SettingsKeys.MusicDir, saved);
        await rig.Settings.SetAsync(SettingsKeys.AnalysisDir, analysis);
        rig.Lifecycle.Start();
        await Eventually(() => rig.Library.Library.Rows.Count == 3);
        var rescans = 0;
        rig.Library.Library.RowsChanged += _ => rescans++;
        AnswerFolders(rig, saved, analysis);

        rig.Folders.Handle(new ChangeFolders(Answer));

        await Eventually(() => rescans >= 1);
        Assert.Equal(saved, rig.Settings.Peek(SettingsKeys.MusicDir));
    }

    // ---- Folders before the output device -------------------------------------------------------

    [Fact]
    public async Task First_run_asks_the_folders_first_and_the_output_device_only_after_they_are_answered()
    {
        var rig = new AppLifecycleRig(devices: [Speakers, Headphones]);
        rig.Library.Bus.Subscribe(new ActionEventHandler<OutputDeviceNeeded>(
            _ => rig.Lifecycle.Handle(new ChooseOutputDevice("Speakers", Answer))));

        rig.Lifecycle.Start();

        // The device list is ready long before the folders are answered; the picker still must not open.
        await Eventually(() => rig.FoldersAsked.Received.Count == 1 && rig.Enumerator.Calls == 1);
        await Task.Delay(150);
        Assert.Empty(rig.DeviceAsked.Received);

        await rig.OnAppThreadAsync(() => rig.Folders.Handle(new ChooseFolders(NewMusicDir(), NewAnalysisDir(), Answer)));

        await Eventually(() => rig.Audio.Started);
        Assert.Single(rig.FoldersAsked.Received);
        Assert.Single(rig.DeviceAsked.Received);
        Assert.Equal("Speakers", rig.Audio.StartedOn);
    }

    [Fact]
    public async Task A_saved_output_device_starts_audio_at_once_without_waiting_for_the_folders_question()
    {
        var rig = new AppLifecycleRig(devices: [Speakers, Headphones]);
        await rig.Settings.SetAsync(SettingsKeys.OutputDevice, "Headphones");

        rig.Lifecycle.Start();

        await Eventually(() => rig.Audio.Started);
        Assert.Equal("Headphones", rig.Audio.StartedOn);
        await Eventually(() => rig.FoldersAsked.Received.Count == 1);  // still open: nobody answered it
        Assert.Empty(rig.DeviceAsked.Received);
    }

    [Fact]
    public async Task Cancelling_the_folders_question_still_lets_the_output_device_picker_ask()
    {
        var rig = new AppLifecycleRig(devices: [Speakers, Headphones]);
        AnswerFolders(rig, null, null);
        rig.Library.Bus.Subscribe(new ActionEventHandler<OutputDeviceNeeded>(
            _ => rig.Lifecycle.Handle(new ChooseOutputDevice("Headphones", Answer))));

        rig.Lifecycle.Start();

        await Eventually(() => rig.Audio.Started);
        Assert.Single(rig.FoldersAsked.Received);
        Assert.Single(rig.DeviceAsked.Received);
        Assert.Equal("Headphones", rig.Audio.StartedOn);
        Assert.Empty(rig.Library.Library.Rows);
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
        await Eventually(() => rig.Audio.StartedOn is not null);
        await Task.Delay(100);  // the route is recorded just after the engine starts
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

    [Fact]
    public async Task A_failed_route_marks_nothing_in_use_and_the_speakers_can_be_picked_again()
    {
        var rig = new AppLifecycleRig(devices: [Speakers, Headphones, ControllerCard], musicDirOverride: NewMusicDir());
        await rig.Settings.SetAsync(SettingsKeys.OutputDevice, "Speakers");
        rig.Audio.NextRoute = FailedRoute;
        rig.Library.Bus.Subscribe(new ActionEventHandler<OutputDeviceNeeded>(
            _ => rig.Lifecycle.Handle(new ChooseOutputDevice("Speakers", Answer))));
        rig.Lifecycle.Start();
        await Eventually(() => rig.Audio.StartedOn is not null);
        await Task.Delay(100);  // the route is recorded just after the engine starts
        rig.DeviceAsked.Received.Clear();

        rig.Lifecycle.Handle(new ChangeOutputDevice(Answer));

        await Eventually(() => rig.Audio.SwitchedTo is not null);
        Assert.Null(rig.DeviceAsked.Received[0].CurrentName);
        Assert.Equal("Speakers", rig.Audio.SwitchedTo);
    }

    [Fact]
    public async Task With_master_on_the_controller_and_no_speaker_chosen_nothing_is_marked_in_use()
    {
        var rig = new AppLifecycleRig(devices: [Speakers, ControllerCard], musicDirOverride: NewMusicDir());
        rig.Library.Bus.Subscribe(new ActionEventHandler<OutputDeviceNeeded>(
            _ => rig.Lifecycle.Handle(new ChooseOutputDevice(null, Answer))));
        rig.Lifecycle.Start();
        await Eventually(() => rig.DeviceAsked.Received.Count == 1);
        await Task.Delay(100);  // the route is recorded just after the engine starts
        rig.DeviceAsked.Received.Clear();

        rig.Lifecycle.Handle(new ChangeOutputDevice(Answer));

        await Eventually(() => rig.DeviceAsked.Received.Count == 1);
        Assert.Null(rig.DeviceAsked.Received[0].CurrentName);
    }

    private static readonly MasterRoute FailedRoute =
        new(AppLifecycleRig.ControllerCard, "route to 'Speakers' failed");

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

    // ---- Retired shortlist: one-time migration into the Track List ---------------------------------

    private static readonly string AlphaPath = LibrarySessionRig.Alpha.FilePath;
    private static readonly string BravoPath = LibrarySessionRig.Bravo.FilePath;

    private static string SavedShortlist(params string[] paths) => System.Text.Json.JsonSerializer.Serialize(paths);

    private static string SavedSongs(AppLifecycleRig rig, params string[] paths) =>
        rig.Codec.Encode(new SavedTrackList(
            paths.Select(p => new SavedTrackListEntry(p, ["songs"])).ToList(),
            [new SavedTrackListSource("songs", TrackListSourceKind.Songs, "Songs")]));

    private static IEnumerable<string> SavedPaths(AppLifecycleRig rig) =>
        rig.Codec.Decode(rig.Settings.Peek(SettingsKeys.TrackList)!).Entries.Select(e => e.Path);

    [Fact]
    public async Task A_saved_shortlist_is_appended_to_the_restored_track_list_without_duplicates()
    {
        var rig = new AppLifecycleRig(musicDirOverride: NewMusicDir());
        await rig.Settings.SetAsync(SettingsKeys.TrackList, SavedSongs(rig, BravoPath));
        await rig.Settings.SetAsync(SettingsKeys.GlanceShortlist, SavedShortlist(AlphaPath, BravoPath));

        rig.Lifecycle.Start();

        await Eventually(() => rig.TrackList.Entries.Count == 2);
        Assert.Equal(new[] { BravoPath, AlphaPath }, rig.TrackList.Entries.Select(e => e.Path));
        Assert.Contains(rig.TrackList.Sources, s => s.Key == "songs");
        await Eventually(() => SavedPaths(rig).SequenceEqual(new[] { BravoPath, AlphaPath }));
    }

    [Fact]
    public async Task The_shortlist_key_is_emptied_once_migrated()
    {
        var rig = new AppLifecycleRig(musicDirOverride: NewMusicDir());
        await rig.Settings.SetAsync(SettingsKeys.TrackList, SavedSongs(rig, BravoPath));
        await rig.Settings.SetAsync(SettingsKeys.GlanceShortlist, SavedShortlist(AlphaPath));

        rig.Lifecycle.Start();

        await Eventually(() => rig.Settings.Peek(SettingsKeys.GlanceShortlist) == "[]");
    }

    [Fact]
    public async Task A_second_startup_does_not_bring_back_a_song_removed_after_the_migration()
    {
        var first = new AppLifecycleRig(musicDirOverride: NewMusicDir());
        await first.Settings.SetAsync(SettingsKeys.TrackList, SavedSongs(first, BravoPath));
        await first.Settings.SetAsync(SettingsKeys.GlanceShortlist, SavedShortlist(AlphaPath));
        first.Lifecycle.Start();
        await Eventually(() => first.Settings.Peek(SettingsKeys.GlanceShortlist) == "[]"
            && SavedPaths(first).SequenceEqual(new[] { BravoPath, AlphaPath }));
        await first.OnAppThreadAsync(() => first.TrackList.Handle(new RemoveFromTrackList(AlphaPath, Answer)));
        await Eventually(() => SavedPaths(first).SequenceEqual(new[] { BravoPath }));

        var second = new AppLifecycleRig(musicDirOverride: NewMusicDir());
        await second.Settings.SetAsync(SettingsKeys.TrackList, first.Settings.Peek(SettingsKeys.TrackList)!);
        await second.Settings.SetAsync(SettingsKeys.GlanceShortlist, first.Settings.Peek(SettingsKeys.GlanceShortlist)!);
        second.Lifecycle.Start();

        await Eventually(() => second.TrackList.Entries.Count == 1);
        await Task.Delay(300);
        Assert.Equal(new[] { BravoPath }, second.TrackList.Entries.Select(e => e.Path));
    }

    [Fact]
    public async Task With_no_shortlist_key_the_track_list_and_settings_are_left_alone()
    {
        var rig = new AppLifecycleRig(musicDirOverride: NewMusicDir());
        await rig.Settings.SetAsync(SettingsKeys.TrackList, SavedSongs(rig, BravoPath));

        rig.Lifecycle.Start();

        await Eventually(() => rig.TrackList.Entries.Count == 1);
        await Task.Delay(300);
        Assert.Equal(new[] { BravoPath }, rig.TrackList.Entries.Select(e => e.Path));
        Assert.Null(rig.Settings.Peek(SettingsKeys.GlanceShortlist));
    }

    // ---- Track List -----------------------------------------------------------------------------

    private static readonly string CharliePath = LibrarySessionRig.Charlie.FilePath;

    private static string SavedList(AppLifecycleRig rig, params string[] paths) =>
        rig.Codec.Encode(new SavedTrackList(
            paths.Select(p => new SavedTrackListEntry(p, ["crate:9"])).ToList(),
            paths.Length == 0 ? [] : [new SavedTrackListSource("crate:9", TrackListSourceKind.Crate, "Warmup")]));

    [Fact]
    public async Task The_saved_track_list_is_restored_in_its_saved_order()
    {
        var rig = new AppLifecycleRig(musicDirOverride: NewMusicDir());
        await rig.Settings.SetAsync(SettingsKeys.TrackList, SavedList(rig, CharliePath, AlphaPath, BravoPath));

        rig.Lifecycle.Start();

        await Eventually(() => rig.TrackList.Entries.Count == 3);
        Assert.Equal(new[] { CharliePath, AlphaPath, BravoPath }, rig.TrackList.Entries.Select(e => e.Path));
        Assert.Equal("Warmup", Assert.Single(rig.TrackList.Sources).Name);
    }

    [Fact]
    public async Task With_no_saved_track_list_All_Tracks_is_loaded_after_the_first_scan()
    {
        var rig = new AppLifecycleRig(musicDirOverride: NewMusicDir());

        rig.Lifecycle.Start();

        await Eventually(() => rig.TrackList.Entries.Count == 3);
        Assert.Equal("All Tracks", Assert.Single(rig.TrackList.Sources).Name);
        Assert.Equal(
            new[] { AlphaPath, BravoPath, CharliePath }.Order(),
            rig.TrackList.Entries.Select(e => e.Path).Order());
    }

    [Fact]
    public async Task A_saved_empty_track_list_stays_empty_after_the_scan()
    {
        var rig = new AppLifecycleRig(musicDirOverride: NewMusicDir());
        await rig.Settings.SetAsync(SettingsKeys.TrackList, SavedList(rig));

        rig.Lifecycle.Start();

        await Eventually(() => rig.Library.Library.Catalog.Count == 3);
        await Task.Delay(300);
        Assert.Empty(rig.TrackList.Entries);
        Assert.Empty(rig.TrackList.Sources);
    }

    [Fact]
    public async Task A_track_list_change_is_saved_as_json_the_codec_reads_back()
    {
        var rig = new AppLifecycleRig(musicDirOverride: NewMusicDir());
        await rig.Settings.SetAsync(SettingsKeys.TrackList, SavedList(rig));
        rig.Lifecycle.Start();
        await Eventually(() => rig.Library.Library.Catalog.Count == 3);

        // Persistence switches on just after the database opens: add until a save shows.
        await Eventually(() =>
        {
            rig.TrackList.Handle(new LoadSongToTrackList(BravoPath, Answer));
            return rig.Settings.Peek(SettingsKeys.TrackList) is { } json && rig.Codec.Decode(json).Entries.Count == 1;
        });

        var saved = rig.Codec.Decode(rig.Settings.Peek(SettingsKeys.TrackList)!);
        Assert.Equal(BravoPath, Assert.Single(saved.Entries).Path);
        Assert.Equal(new[] { "songs" }, saved.Entries[0].SourceKeys);
        var source = Assert.Single(saved.Sources);
        Assert.Equal(("songs", TrackListSourceKind.Songs), (source.Key, source.Kind));
    }

    [Fact]
    public async Task An_unreadable_saved_track_list_starts_empty_with_one_log_line_and_does_not_crash()
    {
        var rig = new AppLifecycleRig(musicDirOverride: NewMusicDir());
        await rig.Settings.SetAsync(SettingsKeys.TrackList, "{ not json");
        var originalOut = Console.Out;
        var captured = new StringWriter();
        Console.SetOut(captured);
        try
        {
            rig.Lifecycle.Start();
            await Eventually(() => rig.Library.Library.Catalog.Count == 3);
            await Task.Delay(300);
        }
        finally { Console.SetOut(originalOut); }

        Assert.Empty(rig.TrackList.Entries);
        var lines = captured.ToString().Split('\n').Where(l => l.Contains("[TrackList] saved list unreadable")).ToList();
        Assert.Single(lines);
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
        await rig.OnAppThreadAsync(() => rig.Library.Deck1.LoadTrack(alpha, alpha.FilePath, [], bpmMultiplier: 1.0));
        await rig.OnAppThreadAsync(() => rig.Library.Deck1.HalveBpm());
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
        await rig.OnAppThreadAsync(() => rig.Library.Deck1.LoadTrack(alpha, alpha.FilePath, [], bpmMultiplier: 1.0));
        await rig.OnAppThreadAsync(() => rig.Library.Deck1.HalveBpm());
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
