using Sholto.App.Lifecycle;
using Sholto.App.Audio;
using Sholto.App.Settings;
using Sholto.Data;

namespace Sholto.App.Tests;

/// <summary>The Track List saved and restored at startup, and the one-time migration of the retired shortlist into it.</summary>
public class TrackListPersistenceTests
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
    public async Task A_cancelled_first_run_folders_question_does_not_load_All_Tracks()
    {
        var rig = new AppLifecycleRig();   // no saved track list = first launch, no saved folder
        rig.Library.Bus.Subscribe(new ActionEventHandler<FoldersNeeded>(
            _ => rig.Folders.Handle(new ChooseFolders(null, null, Answer))));

        rig.Lifecycle.Start();

        await Eventually(() => rig.FoldersAsked.Received.Count == 1);
        await rig.Database.Opened;
        await Task.Delay(300);
        Assert.Empty(rig.TrackList.Entries);
        Assert.Empty(rig.TrackList.Sources);
    }

    [Fact]
    public async Task A_menu_rescan_on_first_launch_does_not_load_All_Tracks()
    {
        var rig = new AppLifecycleRig();
        var dir = NewMusicDir();
        var analysis = Directory.CreateTempSubdirectory("sholto-analysis-").FullName;
        // The first question (FirstRun) is cancelled, the second (Change) is answered with the folders.
        rig.Library.Bus.Subscribe(new ActionEventHandler<FoldersNeeded>(
            e => rig.Folders.Handle(e.Reason == FoldersReason.Change
                ? new ChooseFolders(dir, analysis, Answer)
                : new ChooseFolders(null, null, Answer))));
        rig.Lifecycle.Start();
        await Eventually(() => rig.FoldersAsked.Received.Count == 1);
        await Task.Delay(100);   // let the cancel land: a menu request is ignored while a question is open

        rig.Folders.Handle(new ChangeFolders(Answer));

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
        rig.Lifecycle.Start();
        await Eventually(() => rig.Library.Library.Catalog.Count == 3);
        await Task.Delay(300);

        Assert.Empty(rig.TrackList.Entries);
        Assert.Single(rig.Logs.Lines,
            l => l.Tag == "TrackList" && l.Level == LogLevel.Warn && l.Message.StartsWith("saved list unreadable"));
    }
}
