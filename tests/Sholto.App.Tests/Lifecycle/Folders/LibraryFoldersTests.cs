using Sholto.App.Lifecycle.Folders;
using Sholto.Data;

namespace Sholto.App.Tests;

/// <summary>The music and analysis folders at startup and when the user changes them: which row is in which
/// state, what is asked, what an answer does, and what is never created.</summary>
public class LibraryFoldersTests
{
    // ---- Precedence -----------------------------------------------------------------------------

    [Fact]
    public async Task Both_overrides_are_fixed_used_without_asking_and_not_saved()
    {
        using var rig = new FoldersRig();
        var music = rig.NewDir();
        var cache = rig.NewDir();
        using var withOverrides = new FoldersRig(musicOverride: music, analysisOverride: cache);

        var toScan = await withOverrides.Folders.ResolveAsync();

        Assert.Equal(music, toScan);
        Assert.Equal(cache, withOverrides.Folder.Root);
        Assert.Empty(withOverrides.Needed.Received);
        Assert.Null(withOverrides.Music.Value);
        Assert.Null(withOverrides.Analysis.Value);
    }

    [Fact]
    public async Task Saved_folders_that_exist_are_used_without_asking()
    {
        using var rig = new FoldersRig();
        var music = rig.NewDir();
        var analysis = rig.NewDir();
        using var saved = new FoldersRig(savedMusic: music, savedAnalysis: analysis);

        var toScan = await saved.Folders.ResolveAsync();

        Assert.Equal(music, toScan);
        Assert.Equal(analysis, saved.Folder.Root);
        Assert.Empty(saved.Needed.Received);
    }

    [Fact]
    public async Task A_saved_analysis_folder_that_is_missing_is_asked_about_and_never_created()
    {
        using var rig = new FoldersRig();
        var music = rig.NewDir();
        var gone = rig.NewMissingDir("Sholto Analysis");
        using var saved = new FoldersRig(savedMusic: music, savedAnalysis: gone);
        saved.Answer(null, null);

        var toScan = await saved.Folders.ResolveAsync();

        var asked = Assert.Single(saved.Needed.Received);
        Assert.Equal(FoldersReason.DriveNotFound, asked.Reason);
        Assert.Equal(new FolderRow(music, FolderRowState.Ok, null), asked.Music);
        Assert.Equal(FolderRowState.Missing, asked.Analysis.State);
        Assert.Equal(gone, asked.Analysis.Path);
        Assert.False(Directory.Exists(gone));
        Assert.Null(saved.Folder.Root);
        Assert.Equal(music, toScan);   // the music folder is fine, so the scan still runs
    }

    [Fact]
    public async Task A_saved_music_folder_that_is_missing_asks_and_marks_the_library_unreachable()
    {
        using var rig = new FoldersRig();
        var analysis = rig.NewDir();
        const string gone = "/media/definitely/not/mounted";
        using var saved = new FoldersRig(savedMusic: gone, savedAnalysis: analysis);
        saved.Answer(null, null);

        var toScan = await saved.Folders.ResolveAsync();

        Assert.Equal(FolderRowState.Missing, Assert.Single(saved.Needed.Received).Music.State);
        await saved.Eventually(() => saved.Library.Library.UnreachablePath == gone);
        Assert.Null(toScan);
        Assert.Equal(gone, saved.Music.Value);   // kept for next launch
    }

    [Fact]
    public async Task An_install_with_a_music_folder_but_no_analysis_row_is_an_upgrade_prefilled_with_the_legacy_root()
    {
        using var rig = new FoldersRig();
        var legacy = rig.NewDir();
        new StemTreeBuilder().Add(legacy, "aaaa", bytes: 1000);
        using var upgrading = new FoldersRig(savedMusic: rig.NewDir(), legacyRoot: legacy);
        upgrading.Answer(null, null);

        await upgrading.Folders.ResolveAsync();

        var asked = Assert.Single(upgrading.Needed.Received);
        Assert.Equal(FoldersReason.Upgrade, asked.Reason);
        Assert.Equal(new FolderRow(legacy, FolderRowState.NotChosen, null), asked.Analysis);
        Assert.Equal(4000, asked.AnalysisStemBytes);
    }

    [Fact]
    public async Task Continuing_an_upgrade_keeps_the_legacy_root_in_place_and_saves_it()
    {
        using var rig = new FoldersRig();
        var legacy = rig.NewDir();
        var tree = new StemTreeBuilder().Add(legacy, "aaaa");
        var music = rig.NewDir();
        var mover = new FakeStemFolderMover();
        using var upgrading = new FoldersRig(savedMusic: music, legacyRoot: legacy, mover: mover);
        upgrading.Answer(music, legacy);

        var toScan = await upgrading.Folders.ResolveAsync();

        Assert.Equal(music, toScan);
        Assert.Empty(mover.Calls);
        Assert.True(Directory.Exists(tree));
        Assert.Equal(legacy, upgrading.Folder.Root);
        Assert.Equal(legacy, upgrading.Analysis.Value);
        Assert.Equal(new AnalysisFolderChanged(legacy, false), Assert.Single(upgrading.Changed.Received));
    }

    [Fact]
    public async Task A_fresh_install_suggests_a_sholto_analysis_folder_beside_the_music_folder()
    {
        using var rig = new FoldersRig();
        var parent = rig.NewDir();
        var music = Directory.CreateDirectory(Path.Combine(parent, "Music")).FullName;
        using var fresh = new FoldersRig(musicOverride: music);
        fresh.Answer(null, null);

        await fresh.Folders.ResolveAsync();

        var asked = Assert.Single(fresh.Needed.Received);
        Assert.Equal(FoldersReason.FirstRun, asked.Reason);
        Assert.Equal(new FolderRow(Path.Combine(parent, "Sholto Analysis"), FolderRowState.NotChosen, null), asked.Analysis);
    }

    [Fact]
    public async Task A_fresh_install_falls_back_to_the_legacy_root_when_the_music_folders_parent_is_the_root()
    {
        using var rig = new FoldersRig();
        using var fresh = new FoldersRig(musicOverride: "/sholto-test-music-in-root");
        fresh.Answer(null, null);

        await fresh.Folders.ResolveAsync();

        Assert.Equal(fresh.Legacy, Assert.Single(fresh.Needed.Received).Analysis.Path);
        var suggested = (await fresh.Folders.Handle(new CheckFolders("/sholto-test-music-in-root", null))).SuggestedAnalysis;
        Assert.Equal(fresh.Legacy, suggested);
    }

    [Fact]
    public async Task Cancelling_keeps_the_saved_values_and_leaves_the_analysis_folder_unset()
    {
        using var rig = new FoldersRig();
        var music = rig.NewDir();
        var gone = rig.NewMissingDir();
        using var saved = new FoldersRig(savedMusic: music, savedAnalysis: gone);
        saved.Answer(null, null);

        var toScan = await saved.Folders.ResolveAsync();

        Assert.Equal(music, saved.Music.Value);
        Assert.Equal(gone, saved.Analysis.Value);
        Assert.Null(saved.Folder.Root);
        Assert.Empty(saved.Changed.Received);
        Assert.False(Directory.Exists(gone));
        Assert.Equal(music, toScan);
    }

    // ---- Answers --------------------------------------------------------------------------------

    [Fact]
    public async Task A_first_run_answer_creates_the_analysis_folder_saves_both_and_announces_it_once()
    {
        using var rig = new FoldersRig();
        var parent = rig.NewDir();
        var music = Directory.CreateDirectory(Path.Combine(parent, "Music")).FullName;
        var analysis = Path.Combine(parent, "Sholto Analysis");
        using var fresh = new FoldersRig();
        fresh.Answer(music, analysis);

        var toScan = await fresh.Folders.ResolveAsync();

        Assert.Equal(music, toScan);
        Assert.True(Directory.Exists(analysis));
        Assert.Equal(analysis, fresh.Folder.Root);
        Assert.Equal(music, fresh.Music.Value);
        Assert.Equal(analysis, fresh.Analysis.Value);
        Assert.Equal(new AnalysisFolderChanged(analysis, false), Assert.Single(fresh.Changed.Received));
        Assert.Single(fresh.Needed.Received);
    }

    [Fact]
    public async Task An_answer_with_a_problem_asks_again_with_it_attached_and_changes_nothing()
    {
        using var rig = new FoldersRig();
        var music = rig.NewDir();
        var inside = Path.Combine(music, "stems");
        var good = Path.Combine(rig.NewDir(), "Sholto Analysis");
        using var fresh = new FoldersRig();
        fresh.Answer(music, inside);
        fresh.Answer(music, good);

        await fresh.Folders.ResolveAsync();

        Assert.Equal(2, fresh.Needed.Received.Count);
        Assert.Contains("inside the music folder", fresh.Needed.Received[1].Analysis.Problem);
        Assert.Equal(inside, fresh.Needed.Received[1].Analysis.Path);
        Assert.False(Directory.Exists(inside));
        // Each answer ended in exactly one of: a fresh question, or Changed.
        Assert.Equal(new AnalysisFolderChanged(good, false), Assert.Single(fresh.Changed.Received));
    }

    [Fact]
    public async Task Changing_the_analysis_folder_moves_the_stems_with_the_gate_held_and_progress_reported()
    {
        using var rig = new FoldersRig();
        var old = rig.NewDir();
        var target = rig.NewMissingDir("Sholto Analysis");
        var music = rig.NewDir();
        new StemTreeBuilder().Add(old, "aaaa");
        var mover = new FakeStemFolderMover();
        var gateWasHeld = false;
        using var saved = new FoldersRig(savedMusic: music, savedAnalysis: old, mover: mover);
        mover.Behaviour = async (progress, commit) =>
        {
            using var probe = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
            try { (await saved.Gate.EnterAsync(probe.Token)).Dispose(); }
            catch (OperationCanceledException) { gateWasHeld = true; }
            progress(1, 1);
            await commit();
            return new StemMoveOutcome(true, null);
        };
        await saved.Folders.ResolveAsync();
        saved.Answer(music, target);

        saved.Folders.Handle(new ChangeFolders(saved.Origin));
        await saved.Eventually(() => saved.Changed.Received.Count == 1);

        Assert.Equal([(old, target)], mover.Calls);
        Assert.True(gateWasHeld);
        Assert.Equal(target, saved.Folder.Root);
        Assert.Equal(target, saved.Analysis.Value);
        Assert.Equal(new AnalysisFolderMoveProgress(1, 1), Assert.Single(saved.Progress.Received));
        Assert.Equal(FoldersReason.Change, Assert.Single(saved.Needed.Received).Reason);
        // The gate is free again.
        (await saved.Gate.EnterAsync(new CancellationTokenSource(1000).Token)).Dispose();
    }

    [Fact]
    public async Task A_failed_move_stays_on_the_old_root_and_asks_again_with_the_problem()
    {
        using var rig = new FoldersRig();
        var old = rig.NewDir();
        var target = rig.NewMissingDir("Sholto Analysis");
        var music = rig.NewDir();
        new StemTreeBuilder().Add(old, "aaaa");
        var mover = new FakeStemFolderMover { Behaviour = (_, _) => Task.FromResult(new StemMoveOutcome(false, "Disk full.")) };
        using var saved = new FoldersRig(savedMusic: music, savedAnalysis: old, mover: mover);
        await saved.Folders.ResolveAsync();
        saved.Answer(music, target);

        saved.Folders.Handle(new ChangeFolders(saved.Origin));
        await saved.Eventually(() => saved.Needed.Received.Count == 2);

        Assert.Equal("Disk full.", saved.Needed.Received[1].Analysis.Problem);
        Assert.Empty(saved.Changed.Received);
        Assert.Equal(old, saved.Folder.Root);
        Assert.Equal(old, saved.Analysis.Value);
    }

    [Fact]
    public async Task Choosing_the_music_folder_again_saves_it_and_rescans_even_when_it_is_the_same_one()
    {
        using var rig = new FoldersRig();
        var music = rig.NewDir();
        var analysis = rig.NewDir();
        using var saved = new FoldersRig(savedMusic: music, savedAnalysis: analysis);
        await saved.Folders.ResolveAsync();
        var rescans = 0;
        saved.Library.Library.RowsChanged += _ => rescans++;
        saved.Answer(music, analysis);

        saved.Folders.Handle(new ChangeFolders(saved.Origin));

        await saved.Eventually(() => rescans >= 1);
        Assert.Equal(music, saved.Music.Value);
        Assert.Equal(new AnalysisFolderChanged(analysis, false), Assert.Single(saved.Changed.Received));
    }

    [Fact]
    public async Task Cancelling_a_menu_question_changes_nothing_and_does_not_rescan()
    {
        using var rig = new FoldersRig();
        var music = rig.NewDir();
        var analysis = rig.NewDir();
        using var saved = new FoldersRig(savedMusic: music, savedAnalysis: analysis);
        await saved.Folders.ResolveAsync();
        var rescans = 0;
        saved.Library.Library.RowsChanged += _ => rescans++;
        saved.Answer(null, null);

        saved.Folders.Handle(new ChangeFolders(saved.Origin));
        await saved.Eventually(() => saved.Needed.Received.Count == 1);
        await Task.Delay(100);

        Assert.Equal(0, rescans);
        Assert.Empty(saved.Changed.Received);
        Assert.Equal(analysis, saved.Folder.Root);
    }

    [Fact]
    public async Task A_second_change_request_while_a_question_is_open_is_dropped()
    {
        using var rig = new FoldersRig();
        using var saved = new FoldersRig(savedMusic: rig.NewDir(), savedAnalysis: rig.NewDir());
        await saved.Folders.ResolveAsync();

        saved.Folders.Handle(new ChangeFolders(saved.Origin));
        await saved.Eventually(() => saved.Needed.Received.Count == 1);
        saved.Folders.Handle(new ChangeFolders(saved.Origin));
        await Task.Delay(100);

        Assert.Single(saved.Needed.Received);
    }
}
