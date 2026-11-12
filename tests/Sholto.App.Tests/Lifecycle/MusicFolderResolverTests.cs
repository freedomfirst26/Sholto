using Sholto.App.Lifecycle;
using Sholto.App.Audio;
using Sholto.App.Settings;
using Sholto.Data;

namespace Sholto.App.Tests;

/// <summary>The music-folder questions of the startup sequence: first run, unreachable folder, environment override and the menu.</summary>
public class MusicFolderResolverTests
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
}
