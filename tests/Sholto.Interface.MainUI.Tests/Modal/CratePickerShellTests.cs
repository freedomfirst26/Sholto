using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Sholto.Data;
using Sholto.Interface.MainUI.Controls.Modal;
using Sholto.Interface.MainUI.Theming;
using Sholto.Interface.MainUI.ViewModels;
using Sholto.Interface.MainUI.Views;

namespace Sholto.Interface.MainUI.Tests.Modal;

/// <summary>The crate picker hosted in the shell, with the router in front: typing reaches the query box,
/// Enter adds or creates, Esc and a scrim click close, and the box takes focus on open.</summary>
[Collection(AvaloniaXamlCollection.Name)]
public class CratePickerShellTests
{
    private readonly DataBus _bus = new(new ThrowingFailureSink());
    private readonly F9RecordingCommandHandler<AddTrackToCrate> _added = new();
    private readonly ModalKeyRouter _router = new();
    private readonly CratePickerViewModel _picker;
    private readonly CratePickerOverlay _body = new();
    private readonly ModalShell _shell;
    private readonly TrackRow _row;

    public CratePickerShellTests()
    {
        AvaloniaTestApp.EnsureStarted();
        var crates = new List<CrateRef> { new(1, "All Tracks", 40), new(2, "Warmup", 5) };
        _bus.Register<SearchCrates, Task<IReadOnlyList<CrateRef>>>(new F9QueryHandler<SearchCrates, Task<IReadOnlyList<CrateRef>>>(
            q => Task.FromResult<IReadOnlyList<CrateRef>>(
                crates.Where(c => c.Name.Contains(q.Query, StringComparison.OrdinalIgnoreCase)).ToList())));
        _bus.Register<AddTrackToCrate>(_added);
        _picker = new CratePickerViewModel(_bus, _bus, new ImmediateAppThread());
        _row = new TrackRowFactory(new ThemeStackFactory().Build().Context).Create(
            new TrackSummary("/music/a.mp3", "Alpha", "Zed", TimeSpan.FromMinutes(3)) { TrackId = Guid.NewGuid() });
        _shell = new ModalShell { Modal = _picker, Body = _body };
    }

    private bool Route(Key key) => _router.Route(_picker, key, KeyModifiers.None);

    [Fact]
    public async Task The_query_box_is_the_focus_target_and_the_body_is_bound_to_the_picker()
    {
        await _picker.OpenAsync(_row);

        Assert.Same(_body.FindControl<TextBox>("CrateQueryBox"), _shell.FindFocusTarget());
    }

    [Fact]
    public async Task Letters_and_backspace_are_left_for_the_query_box()
    {
        await _picker.OpenAsync(_row);

        Assert.False(Route(Key.A));
        Assert.False(Route(Key.Back));
        Assert.True(_picker.IsOpen);
    }

    [Fact]
    public async Task Down_moves_and_enter_adds_to_the_highlighted_crate_then_closes()
    {
        await _picker.OpenAsync(_row);

        Assert.True(Route(Key.Down));
        Assert.True(Route(Key.Enter));

        var sent = Assert.Single(_added.Received);
        Assert.Equal(2, sent.CrateId);
        Assert.False(sent.Create);
        Assert.False(_picker.IsOpen);
    }

    [Fact]
    public async Task Enter_on_the_create_row_creates_the_typed_crate()
    {
        await _picker.OpenAsync(_row);
        _picker.Query = "Fresh";

        Assert.Equal("Create", _picker.Buttons.Primary);
        Assert.True(Route(Key.Enter));

        var sent = Assert.Single(_added.Received);
        Assert.True(sent.Create);
        Assert.Equal("Fresh", sent.CrateName);
    }

    [Fact]
    public async Task Escape_closes_and_so_does_a_scrim_click()
    {
        await _picker.OpenAsync(_row);
        Assert.True(Route(Key.Escape));
        Assert.False(_picker.IsOpen);

        await _picker.OpenAsync(_row);
        var scrim = _shell.FindControl<Grid>("Scrim")!;
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
        scrim.RaiseEvent(new Avalonia.Input.PointerPressedEventArgs(scrim, pointer, new Border(), default, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
            KeyModifiers.None) { RoutedEvent = InputElement.PointerPressedEvent });
        Assert.False(_picker.IsOpen);
    }
}
