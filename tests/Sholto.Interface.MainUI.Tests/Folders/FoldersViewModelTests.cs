using Microsoft.Extensions.Options;
using Sholto.Data;
using Sholto.Interface.MainUI.Controls.Modal;
using Sholto.Interface.MainUI.ViewModels;

namespace Sholto.Interface.MainUI.Tests;

/// <summary>The Folders panel's view model: two independent rows, a checked pair, Continue only when both are
/// fine, and a panel that stays open and ignores dismiss until the App answers.</summary>
public class FoldersViewModelTests
{
    private const string Suggested = "/media/Data/Sholto Analysis";

    private readonly DataBus _bus = new(new ThrowingFailureSink());
    private readonly List<ChooseFolders> _answers = [];
    private readonly List<CheckFolders> _checks = [];
    private Func<CheckFolders, Task<FolderCheckResult>> _check;
    private readonly FoldersViewModel _vm;

    public FoldersViewModelTests()
    {
        _check = q => Task.FromResult(new FolderCheckResult(null, null, q.Music is null ? null : Suggested));
        _bus.Register(new AnswerRecorder(_answers));
        _bus.Register(new F9QueryHandler<CheckFolders, Task<FolderCheckResult>>(q =>
        {
            _checks.Add(q);
            return _check(q);
        }));
        _vm = new FoldersViewModel(_bus, _bus, _bus, new FolderRowViewModelFactory(), Options.Create(new FoldersPanelOptions()), new RecordingLogFactory());
    }

    private sealed class AnswerRecorder(List<ChooseFolders> sent) : ICommandHandler<ChooseFolders>
    {
        private readonly List<ChooseFolders> _sent = sent;
        public void Handle(in ChooseFolders command) => _sent.Add(command);
    }

    private static FolderRow Ok(string path) => new(path, FolderRowState.Ok, null);

    private static FoldersNeeded Needed(FoldersReason reason, FolderRow music, FolderRow analysis, long bytes = 0) =>
        new(reason, music, analysis, bytes);

    private void ShowFirstRun() => _vm.Show(Needed(FoldersReason.FirstRun,
        new FolderRow(null, FolderRowState.NotChosen, null),
        new FolderRow(null, FolderRowState.NotChosen, null)));

    private void ShowChange() => _vm.Show(Needed(FoldersReason.Change, Ok("/music"), Ok("/analysis")));

    [Fact]
    public void Picking_music_moves_an_analysis_default_nobody_picked()
    {
        ShowFirstRun();
        _vm.Pick(_vm.Music, "/media/Data/Music");
        Assert.Equal("/media/Data/Music", _vm.Music.Path);
        Assert.Equal(Suggested, _vm.Analysis.Path);
    }

    [Fact]
    public void Picking_analysis_then_music_leaves_analysis_alone()
    {
        ShowFirstRun();
        _vm.Pick(_vm.Analysis, "/fast/analysis");
        _vm.Pick(_vm.Music, "/media/Data/Music");
        Assert.Equal("/fast/analysis", _vm.Analysis.Path);
        Assert.Equal("/media/Data/Music", _vm.Music.Path);
    }

    [Fact]
    public void Picking_one_row_never_changes_the_other_when_both_are_set()
    {
        ShowChange();
        _vm.Pick(_vm.Music, "/other/music");
        Assert.Equal("/analysis", _vm.Analysis.Path);
        _vm.Pick(_vm.Analysis, "/other/analysis");
        Assert.Equal("/other/music", _vm.Music.Path);
    }

    [Fact]
    public void Continue_needs_both_paths()
    {
        ShowFirstRun();
        Assert.False(_vm.CanConfirm);
        _vm.Pick(_vm.Music, "/media/Data/Music");
        Assert.True(_vm.CanConfirm);
    }

    [Fact]
    public void Continue_is_disabled_while_a_row_has_a_problem()
    {
        _check = q => Task.FromResult(new FolderCheckResult(null, "Inside the music folder.", null));
        ShowChange();
        Assert.True(_vm.CanConfirm);
        _vm.Pick(_vm.Analysis, "/music/stems");
        Assert.Equal("Inside the music folder.", _vm.Analysis.Problem);
        Assert.False(_vm.CanConfirm);

        _check = q => Task.FromResult(new FolderCheckResult("Not found.", null, null));
        _vm.Pick(_vm.Analysis, "/fine");
        Assert.Equal("Not found.", _vm.Music.Problem);
        Assert.False(_vm.CanConfirm);
    }

    [Fact]
    public void Continue_is_disabled_while_a_check_is_pending_and_the_latest_answer_wins()
    {
        var first = new TaskCompletionSource<FolderCheckResult>();
        var second = new TaskCompletionSource<FolderCheckResult>();
        var queue = new Queue<TaskCompletionSource<FolderCheckResult>>([first, second]);
        _check = _ => queue.Dequeue().Task;
        ShowChange();

        _vm.Pick(_vm.Music, "/one");
        _vm.Pick(_vm.Music, "/two");
        Assert.False(_vm.CanConfirm);

        second.SetResult(new FolderCheckResult(null, null, null));
        Assert.True(_vm.CanConfirm);
        first.SetResult(new FolderCheckResult("Stale problem.", null, null));
        Assert.Null(_vm.Music.Problem);
        Assert.True(_vm.CanConfirm);
    }

    [Fact]
    public void A_fixed_row_cannot_be_changed()
    {
        _vm.Show(Needed(FoldersReason.Change, new FolderRow("/env/music", FolderRowState.Fixed, null), Ok("/analysis")));
        Assert.False(_vm.Music.CanChange);
        Assert.Equal("Set by SHOLTO_MUSIC_DIR", _vm.Music.ChangeLabel);
        Assert.True(_vm.Analysis.CanChange);
        Assert.Equal("Change…", _vm.Analysis.ChangeLabel);

        _vm.Pick(_vm.Music, "/elsewhere");
        Assert.Equal("/env/music", _vm.Music.Path);

        _vm.Show(Needed(FoldersReason.Change, Ok("/music"), new FolderRow("/env/cache", FolderRowState.Fixed, null)));
        Assert.False(_vm.Analysis.CanChange);
        Assert.Equal("Set by SHOLTO_CACHE_ROOT", _vm.Analysis.ChangeLabel);
    }

    [Fact]
    public void Confirm_sends_both_paths_and_stays_open_working()
    {
        ShowChange();
        _vm.Pick(_vm.Music, "/new/music");
        _vm.Confirm();

        var sent = Assert.Single(_answers);
        Assert.Equal("/new/music", sent.Music);
        Assert.Equal("/analysis", sent.Analysis);
        Assert.Equal(InterfaceIds.MainUI, sent.Origin.InterfaceId);
        Assert.True(_vm.IsOpen);
        Assert.True(_vm.IsWorking);
        Assert.False(_vm.CanConfirm);
        Assert.False(_vm.Music.CanChange);
    }

    [Fact]
    public void Confirm_does_nothing_while_continue_is_disabled()
    {
        ShowFirstRun();
        _vm.Confirm();
        Assert.Empty(_answers);
        Assert.False(_vm.IsWorking);
    }

    [Fact]
    public void The_apps_folder_changed_closes_the_panel()
    {
        ShowChange();
        _vm.Confirm();
        _bus.Publish(new AnalysisFolderChanged("/analysis", false));
        Assert.False(_vm.IsOpen);
        Assert.False(_vm.IsWorking);
    }

    [Fact]
    public void A_folder_changed_while_closed_does_not_open_anything()
    {
        _bus.Publish(new AnalysisFolderChanged("/analysis", false));
        Assert.False(_vm.IsOpen);
    }

    [Fact]
    public void Needed_with_a_problem_shows_it_and_a_fresh_question_ends_the_working_state()
    {
        ShowChange();
        _vm.Confirm();
        Assert.True(_vm.IsWorking);

        _vm.Show(Needed(FoldersReason.Change, Ok("/music"), new FolderRow("/analysis", FolderRowState.Ok, "Not writable.")));
        Assert.True(_vm.IsOpen);
        Assert.False(_vm.IsWorking);
        Assert.Equal("Not writable.", _vm.Analysis.Problem);
        Assert.True(_vm.Analysis.HasProblem);
        Assert.False(_vm.CanConfirm);
    }

    [Fact]
    public void A_missing_row_blocks_continue_until_it_is_picked()
    {
        _vm.Show(Needed(FoldersReason.DriveNotFound, new FolderRow("/media/gone", FolderRowState.Missing, null), Ok("/analysis")));
        Assert.True(_vm.Music.HasProblem);
        Assert.False(_vm.CanConfirm);
        _vm.Pick(_vm.Music, "/media/back");
        Assert.True(_vm.CanConfirm);
    }

    [Fact]
    public void Progress_is_shown_as_done_of_total_while_working()
    {
        ShowChange();
        _vm.Confirm();
        Assert.Equal("Applying…", _vm.StatusText);
        _bus.Publish(new AnalysisFolderMoveProgress(3, 12));
        Assert.Equal("Moving stems: 3 of 12 track folders", _vm.StatusText);
    }

    [Fact]
    public void Dismiss_is_ignored_while_moving()
    {
        ShowChange();
        _vm.Confirm();
        _bus.Publish(new AnalysisFolderMoveProgress(1, 4));
        _vm.Dismiss();
        Assert.True(_vm.IsOpen);
        Assert.Single(_answers);
    }

    [Fact]
    public void Dismiss_closes_and_answers_with_both_paths_null()
    {
        ShowChange();
        _vm.Dismiss();
        Assert.False(_vm.IsOpen);
        var sent = Assert.Single(_answers);
        Assert.Null(sent.Music);
        Assert.Null(sent.Analysis);
    }

    [Theory]
    [InlineData(FoldersReason.FirstRun, ModalScrimClick.Ignored, "Not now")]
    [InlineData(FoldersReason.Upgrade, ModalScrimClick.Ignored, "Not now")]
    [InlineData(FoldersReason.DriveNotFound, ModalScrimClick.Ignored, "Not now")]
    [InlineData(FoldersReason.Change, ModalScrimClick.Dismisses, "Cancel")]
    public void The_scrim_and_dismiss_button_follow_the_reason(FoldersReason reason, ModalScrimClick scrim, string dismiss)
    {
        _vm.Show(Needed(reason, Ok("/music"), Ok("/analysis")));
        Assert.Equal(scrim, _vm.ScrimClick);
        Assert.Equal(new ModalButtons(dismiss, null, "Continue"), _vm.Buttons);
    }

    [Fact]
    public void Upgrade_shows_the_current_analysis_folder_and_its_size()
    {
        _vm.Show(Needed(FoldersReason.Upgrade, Ok("/music"),
            new FolderRow("/home/s/.local/share/sholto", FolderRowState.Ok, null), 5L * 1024 * 1024 * 1024));
        Assert.Contains("/home/s/.local/share/sholto", _vm.Subtitle);
        Assert.Contains("5.0 GB", _vm.Subtitle);
        Assert.Contains("5.0 GB of stems here", _vm.Analysis.Detail);
    }

    [Fact]
    public void An_upgrade_keeps_its_prefilled_analysis_folder_when_music_is_picked()
    {
        _vm.Show(Needed(FoldersReason.Upgrade, Ok("/music"), new FolderRow("/legacy", FolderRowState.NotChosen, null)));
        _vm.Pick(_vm.Music, "/music2");
        Assert.Equal("/legacy", _vm.Analysis.Path);
    }
}
