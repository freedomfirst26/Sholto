using Sholto.App.Audio;
using Sholto.App.Settings;
using Sholto.Data;

namespace Sholto.App.Tests;

/// <summary>The folders questions of the startup sequence, through the whole lifecycle: first run, an unreachable
/// (unmounted) folder, the environment overrides, the menu, and the folders question coming before the output
/// device's. The cases the music folder resolver had, moved here when the library folders replaced it.</summary>
public class FolderStartupTests
{
    private static readonly Origin Answer = AppLifecycleRig.Origin;
    private const string Gone = "/media/definitely/not/mounted";

    private static readonly AudioDevice Speakers = new("Speakers", IsDefault: true);
    private static readonly AudioDevice Headphones = new("Headphones", IsDefault: false);

    private static string NewMusicDir() => Directory.CreateTempSubdirectory("sholto-music-").FullName;

    private static string NewAnalysisDir() => Directory.CreateTempSubdirectory("sholto-analysis-").FullName;

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

    /// <summary>The "interface" answering every <see cref="FoldersNeeded"/> with this pick (both null cancels).</summary>
    private static void AnswerFolders(AppLifecycleRig rig, string? music, string? analysis) =>
        rig.Library.Bus.Subscribe(new ActionEventHandler<FoldersNeeded>(
            _ => rig.Folders.Handle(new ChooseFolders(music, analysis, Answer))));

    // ---- Startup --------------------------------------------------------------------------------

    [Fact]
    public async Task With_the_database_and_reachable_saved_folders_the_library_is_scanned_with_its_stores_and_nothing_is_asked()
    {
        var rig = new AppLifecycleRig();
        var analysis = NewAnalysisDir();
        await rig.Settings.SetAsync(SettingsKeys.MusicDir, NewMusicDir());
        await rig.Settings.SetAsync(SettingsKeys.AnalysisDir, analysis);

        rig.Lifecycle.Start();

        await Eventually(() => rig.Library.Library.Rows.Count == 3);
        Assert.Empty(rig.FoldersAsked.Received);
        Assert.All(rig.Library.Library.Rows, r => Assert.NotEqual(Guid.Empty, r.TrackId));
        Assert.False(rig.Library.Library.IsUnreachable);
        Assert.Equal(analysis, rig.AnalysisFolder.Root);
    }

    [Fact]
    public async Task The_environment_overrides_are_used_without_asking_even_when_nothing_is_saved()
    {
        var analysis = NewAnalysisDir();
        var rig = new AppLifecycleRig(musicDirOverride: NewMusicDir(), analysisDirOverride: analysis);

        rig.Lifecycle.Start();

        await Eventually(() => rig.Library.Library.Rows.Count == 3);
        Assert.Empty(rig.FoldersAsked.Received);
        Assert.Equal(analysis, rig.AnalysisFolder.Root);
        Assert.Null(rig.Settings.Peek(SettingsKeys.AnalysisDir));
    }

    [Fact]
    public async Task A_fresh_profile_asks_for_both_folders_then_saves_them_and_scans()
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
        Assert.Equal(FolderRowState.NotChosen, asked.Music.State);
        Assert.Equal(FolderRowState.NotChosen, asked.Analysis.State);
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
        Assert.Null(rig.AnalysisFolder.Root);
    }

    [Fact]
    public async Task An_unmounted_music_drive_shows_the_banner_and_asks_again_naming_the_missing_path()
    {
        var rig = new AppLifecycleRig();
        await rig.Settings.SetAsync(SettingsKeys.MusicDir, Gone);
        await rig.Settings.SetAsync(SettingsKeys.AnalysisDir, NewAnalysisDir());

        rig.Lifecycle.Start();

        await Eventually(() => rig.FoldersAsked.Received.Count == 1);
        var asked = rig.FoldersAsked.Received[0];
        Assert.Equal(FoldersReason.DriveNotFound, asked.Reason);
        Assert.Equal(Gone, asked.Music.Path);
        Assert.Equal(FolderRowState.Missing, asked.Music.State);
        Assert.Equal(FolderRowState.Ok, asked.Analysis.State);
        Assert.Equal(Gone, rig.Library.Library.UnreachablePath);
        Assert.Empty(rig.Library.Library.Rows);
    }

    [Fact]
    public async Task An_unmounted_analysis_drive_asks_again_and_is_never_created()
    {
        var rig = new AppLifecycleRig();
        await rig.Settings.SetAsync(SettingsKeys.MusicDir, NewMusicDir());
        await rig.Settings.SetAsync(SettingsKeys.AnalysisDir, Gone);
        AnswerFolders(rig, null, null);

        rig.Lifecycle.Start();

        await Eventually(() => rig.FoldersAsked.Received.Count == 1);
        var asked = rig.FoldersAsked.Received[0];
        Assert.Equal(FoldersReason.DriveNotFound, asked.Reason);
        Assert.Equal(FolderRowState.Missing, asked.Analysis.State);
        Assert.Equal(Gone, asked.Analysis.Path);
        // Cancelled: the music folder that is there is still scanned; stems stay off until a folder is chosen.
        await Eventually(() => rig.Library.Library.Rows.Count == 3);
        Assert.False(Directory.Exists(Gone));
        Assert.Null(rig.AnalysisFolder.Root);
        Assert.Equal(Gone, rig.Settings.Peek(SettingsKeys.AnalysisDir));
    }

    [Fact]
    public async Task Cancelling_the_question_for_an_unreachable_folder_keeps_the_saved_path_for_next_launch()
    {
        var rig = new AppLifecycleRig();
        await rig.Settings.SetAsync(SettingsKeys.MusicDir, Gone);
        await rig.Settings.SetAsync(SettingsKeys.AnalysisDir, NewAnalysisDir());
        AnswerFolders(rig, null, null);

        rig.Lifecycle.Start();

        await Eventually(() => rig.FoldersAsked.Received.Count == 1);
        await Task.Delay(100);
        Assert.Equal(Gone, rig.Settings.Peek(SettingsKeys.MusicDir));
        Assert.Empty(rig.Library.Library.Rows);
        Assert.True(rig.Library.Library.IsUnreachable);
    }

    [Fact]
    public async Task Choosing_a_folder_for_an_unreachable_one_replaces_the_saved_path_and_clears_the_banner()
    {
        var rig = new AppLifecycleRig();
        var analysis = NewAnalysisDir();
        await rig.Settings.SetAsync(SettingsKeys.MusicDir, Gone);
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
        Assert.Null(await rig.AnalysisDirPreference.GetAsync());
        Assert.All(rig.Library.Library.Rows, r => Assert.Equal(Guid.Empty, r.TrackId));
    }

    // ---- The menu -------------------------------------------------------------------------------

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
}
