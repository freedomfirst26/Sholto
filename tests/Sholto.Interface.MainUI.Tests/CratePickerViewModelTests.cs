using Avalonia.Input;
using Sholto.Data;
using Sholto.Interface.MainUI.Controls.Modal;
using Sholto.Interface.MainUI.Theming;
using Sholto.Interface.MainUI.ViewModels;

namespace Sholto.Interface.MainUI.Tests;

/// <summary>The add-to-crate chooser over a bus: crates come from the SearchCrates query, a "create" row
/// appears for a name that is not a crate yet, and committing sends AddTrackToCrate and closes.</summary>
public class CratePickerViewModelTests
{
    private readonly DataBus _bus = new(new ThrowingFailureSink());
    private readonly List<CrateRef> _crates =
    [
        new CrateRef(1, "All Tracks", 40),
        new CrateRef(2, "Warmup", 5),
        new CrateRef(3, "Peak Time", 9),
    ];
    private readonly F9QueryHandler<SearchCrates, Task<IReadOnlyList<CrateRef>>> _search;
    private readonly F9RecordingCommandHandler<AddTrackToCrate> _added = new();
    private readonly CratePickerViewModel _picker;
    private readonly TrackRow _row;

    public CratePickerViewModelTests()
    {
        AvaloniaTestApp.EnsureStarted();
        _search = new(query => Task.FromResult<IReadOnlyList<CrateRef>>(
            _crates.Where(c => c.Name.Contains(query.Query, StringComparison.OrdinalIgnoreCase)).ToList()));
        _bus.Register<SearchCrates, Task<IReadOnlyList<CrateRef>>>(_search);
        _bus.Register<AddTrackToCrate>(_added);
        _picker = new CratePickerViewModel(_bus, _bus, new ImmediateAppThread());
        _row = new TrackRowFactory(new ThemeStackFactory().Build().Context).Create(
            new TrackSummary("/music/a.mp3", "Alpha", "Zed", TimeSpan.FromMinutes(3)) { TrackId = Guid.NewGuid() });
    }

    [Fact]
    public async Task Opening_for_a_track_lists_every_crate_and_no_create_row()
    {
        await _picker.OpenAsync(_row);

        Assert.Same(_row, _picker.Row);
        Assert.Equal("Zed — Alpha", _picker.Title);
        Assert.Equal(["All Tracks", "Warmup", "Peak Time"], _picker.Options.Select(o => o.Display));
        Assert.DoesNotContain(_picker.Options, o => o.IsCreate);
        Assert.Equal(0, _picker.SelectedIndex);
    }

    [Fact]
    public async Task An_option_carries_the_crates_id_and_track_count()
    {
        await _picker.OpenAsync(_row);

        var warmup = _picker.Options[1];
        Assert.Equal(2, warmup.CrateId);
        Assert.Equal(5, warmup.TrackCount);
        Assert.False(warmup.IsCreate);
    }

    [Fact]
    public async Task Typing_filters_the_crates_through_the_query_with_the_trimmed_text()
    {
        await _picker.OpenAsync(_row);

        _picker.Query = "  warm ";

        Assert.Equal("warm", _search.Asked[^1].Query);
        Assert.Contains(_picker.Options, o => o.Display == "Warmup");
        Assert.DoesNotContain(_picker.Options, o => o.Display == "Peak Time");
    }

    [Fact]
    public async Task A_name_that_is_not_a_crate_puts_a_create_row_first()
    {
        await _picker.OpenAsync(_row);

        _picker.Query = "Fresh";

        Assert.True(_picker.Options[0].IsCreate);
        Assert.Contains("Fresh", _picker.Options[0].Display);
        Assert.Equal(0, _picker.SelectedIndex);
    }

    [Fact]
    public async Task A_name_matching_an_existing_crate_in_any_case_offers_no_create_row()
    {
        await _picker.OpenAsync(_row);

        _picker.Query = "warmup";

        Assert.DoesNotContain(_picker.Options, o => o.IsCreate);
        Assert.Equal(["Warmup"], _picker.Options.Select(o => o.Display));
    }

    [Fact]
    public async Task At_most_five_matching_crates_are_listed()
    {
        _crates.AddRange(
        [
            new CrateRef(4, "D", 1), new CrateRef(5, "E", 1), new CrateRef(6, "F", 1), new CrateRef(7, "G", 1),
        ]);

        await _picker.OpenAsync(_row);

        Assert.Equal(5, _picker.Options.Count);
    }

    [Fact]
    public async Task Move_wraps_around_the_options()
    {
        await _picker.OpenAsync(_row);

        _picker.Move(-1);
        Assert.Equal(2, _picker.SelectedIndex);

        _picker.Move(1);
        Assert.Equal(0, _picker.SelectedIndex);
    }

    [Fact]
    public async Task Committing_an_existing_crate_sends_AddTrackToCrate_without_create_and_closes()
    {
        await _picker.OpenAsync(_row);
        _picker.Move(1);
        var closed = 0;
        _picker.RequestClose += () => closed++;

        await _picker.CommitAsync();

        var command = Assert.Single(_added.Received);
        Assert.Equal(_row.TrackId, command.TrackId);
        Assert.Equal(2, command.CrateId);
        Assert.Equal("Warmup", command.CrateName);
        Assert.False(command.Create);
        Assert.Equal(InterfaceIds.MainUI, command.Origin.InterfaceId);
        Assert.Equal(1, closed);
    }

    [Fact]
    public async Task Committing_the_create_row_sends_AddTrackToCrate_with_create_and_the_typed_name()
    {
        await _picker.OpenAsync(_row);
        _picker.Query = "  Fresh ";
        var closed = 0;
        _picker.RequestClose += () => closed++;

        await _picker.CommitAsync();

        var command = Assert.Single(_added.Received);
        Assert.Equal(_row.TrackId, command.TrackId);
        Assert.Equal(0, command.CrateId);
        Assert.Equal("Fresh", command.CrateName);
        Assert.True(command.Create);
        Assert.Equal(InterfaceIds.MainUI, command.Origin.InterfaceId);
        Assert.Equal(1, closed);
    }

    [Fact]
    public async Task Committing_before_a_track_is_opened_sends_nothing_and_does_not_close()
    {
        var closed = 0;
        _picker.RequestClose += () => closed++;

        await _picker.CommitAsync();

        Assert.Empty(_added.Received);
        Assert.Equal(0, closed);
    }

    [Fact]
    public async Task Committing_with_no_options_sends_nothing()
    {
        _crates.Clear();
        await _picker.OpenAsync(_row);
        var closed = 0;
        _picker.RequestClose += () => closed++;

        await _picker.CommitAsync();

        Assert.Empty(_picker.Options);
        Assert.Empty(_added.Received);
        Assert.Equal(0, closed);
    }

    [Fact]
    public void Close_raises_RequestClose()
    {
        var closed = 0;
        _picker.RequestClose += () => closed++;

        _picker.Close();

        Assert.Equal(1, closed);
    }

    [Fact]
    public async Task Opening_again_clears_the_previous_query()
    {
        await _picker.OpenAsync(_row);
        _picker.Query = "Fresh";

        await _picker.OpenAsync(_row);

        Assert.Equal("", _picker.Query);
        Assert.DoesNotContain(_picker.Options, o => o.IsCreate);
    }

    [Fact]
    public async Task Opening_sets_IsOpen_and_Close_and_Dismiss_clear_it()
    {
        Assert.False(_picker.IsOpen);
        await _picker.OpenAsync(_row);
        Assert.True(_picker.IsOpen);

        _picker.Dismiss();

        Assert.False(_picker.IsOpen);
    }

    [Fact]
    public async Task Committing_clears_IsOpen()
    {
        await _picker.OpenAsync(_row);

        await _picker.CommitAsync();

        Assert.False(_picker.IsOpen);
    }

    [Fact]
    public void The_modal_captures_text_is_narrow_and_labelled_for_crates()
    {
        Assert.True(_picker.CapturesText);
        Assert.Equal(ModalWidth.Narrow, _picker.Width);
        Assert.Equal("📦  ADD TO CRATE", _picker.Eyebrow);
        Assert.False(_picker.CanGoBack);
    }

    [Fact]
    public async Task Buttons_read_Add_on_a_crate_and_Create_on_the_create_row()
    {
        await _picker.OpenAsync(_row);
        Assert.Equal(new ModalButtons("Cancel", null, "Add"), _picker.Buttons);

        _picker.Query = "Fresh";

        Assert.Equal(new ModalButtons("Cancel", null, "Create"), _picker.Buttons);
    }

    [Fact]
    public async Task The_Buttons_change_is_announced_when_the_highlight_moves_onto_the_create_row()
    {
        await _picker.OpenAsync(_row);
        _picker.Query = "a";
        _picker.Move(1);
        var changed = new List<string?>();
        _picker.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        _picker.Move(-1);

        Assert.Contains(nameof(CratePickerViewModel.Buttons), changed);
    }

    [Fact]
    public async Task CanConfirm_is_true_only_while_there_are_options()
    {
        _crates.Clear();
        await _picker.OpenAsync(_row);
        Assert.False(_picker.CanConfirm);

        _picker.Query = "Fresh";

        Assert.True(_picker.CanConfirm);
    }

    [Fact]
    public async Task Confirm_adds_the_highlighted_crate()
    {
        await _picker.OpenAsync(_row);
        _picker.Move(2);

        _picker.Confirm();

        var command = Assert.Single(_added.Received);
        Assert.Equal(3, command.CrateId);
        Assert.False(_picker.IsOpen);
    }

    [Fact]
    public async Task Arrow_keys_move_the_highlight_and_other_keys_are_left_to_the_text_box()
    {
        await _picker.OpenAsync(_row);

        Assert.True(_picker.HandleKey(Key.Down, KeyModifiers.None));
        Assert.Equal(1, _picker.SelectedIndex);
        Assert.True(_picker.HandleKey(Key.Up, KeyModifiers.None));
        Assert.Equal(0, _picker.SelectedIndex);
        Assert.False(_picker.HandleKey(Key.A, KeyModifiers.None));
        Assert.False(_picker.HandleKey(Key.Back, KeyModifiers.None));
    }
}
