using Microsoft.Extensions.Options;
using Sholto.Interface.MainUI.Controls.CollapseToIcon;
using Sholto.Interface.MainUI.Controls.Knob;
using Sholto.Interface.MainUI.Controls.Modal;
using Sholto.Interface.MainUI.Controls.WaveformStyles;
using Sholto.App.Analysis.Analyzers.Segments;
using Sholto.App.Analysis.Analyzers.Waveform;
using Sholto.App.Audio;
using Sholto.App.Library;
using Sholto.Data;
using Sholto.Interface.MainUI.Models;
using Sholto.Interface.MainUI.Theming;
using Sholto.Interface.MainUI.ViewModels;
using Sholto.Interface.MainUI.ViewModels.Glance;

namespace Sholto.Interface.MainUI.Tests;

/// <summary>The main view model as a projection of the bus: events in become properties and toasts, every
/// action out is one command. Built over a real bus with the real library rows, search, overlay factory and
/// deck view models; the app thread is immediate so nothing touches Avalonia's dispatcher.</summary>
public class MainViewModelTests
{
    private readonly DataBus _bus = new(new ThrowingFailureSink());
    private readonly List<string?> _changed = [];
    private readonly MainViewModel _vm;

    public MainViewModelTests()
    {
        AvaloniaTestApp.EnsureStarted();
        var themes = new ThemeStackFactory().Build();
        var appThread = new ImmediateAppThread();
        var recency = new TagRecency();
        var rows = new LibraryRowsViewModel(_bus, new TrackRowFactory(themes.Context));
        var clock = new FakeFrameClock();
        var deck1 = new DeckViewModel(0, _bus, _bus, themes.Context, new NoPeaksFactory(),
            new DiscBloomFactory(new ManualFrameClock()));
        var deck2 = new DeckViewModel(1, _bus, _bus, themes.Context, new NoPeaksFactory(),
            new DiscBloomFactory(new ManualFrameClock()));
        var clocks = new DeckViewModelClockSource(deck1, deck2);
        var glance = new GlanceViewModel(_bus, _bus, _bus, appThread, clock, rows, recency,
            new GlanceHeaderViewModel(clocks, clock, _bus, new DeckSlotFactory(clocks, new FixedMotionPreference(false), Options.Create(new GlanceViewOptions()))),
            new FixedMotionPreference(false), Options.Create(new GlanceViewOptions()));
        var waveformStyle = new WaveformStyleViewModel(new WaveformStylesFactory(Options.Create(new WaveformStyleOptions())).Create(), _bus);
        var themeViewModel = new ThemeViewModel(themes.Context, themes.Catalog, _bus);
        _vm = new MainViewModel(
            themeViewModel,
            new OverlayViewModelFactory(recency, _bus, _bus, _bus, appThread),
            rows,
            _bus,
            _bus,
            appThread,
            glance,
            new LoadFeedbackViewModel(_bus, _bus),
            new TrackActionsViewModel(),
            deck1,
            deck2,
            waveformStyle,
            new LayoutWizardViewModel(waveformStyle, new WaveformStyleOptionFactory(), new WaveformPreviewRenderer(),
                new DemoWaveformFactory(), new WaveformPreviewScroll(new ManualFrameClock()), appThread,
                themeViewModel, new ThemeOptionFactory()),
            new SettingsViewModel(_bus, _bus, new KnobScaleFactory()),
            new SystemReportViewModel(),
            new CollapseToIconSequence(new FakeFrameClock(), new FixedMotionPreference(false),
                new CollapseToIconOptions("faceplate", new CollapseToIconTimingsFactory().Standard()),
                new AlwaysHintPolicy()),
            new TrackListViewModel(_bus, _bus));
        _vm.PropertyChanged += (_, e) => _changed.Add(e.PropertyName);

        // The search overlay and overlay factory ask these once the database is attached.
        _bus.Register<SearchCrates, Task<IReadOnlyList<CrateRef>>>(
            new F9QueryHandler<SearchCrates, Task<IReadOnlyList<CrateRef>>>(
                _ => Task.FromResult<IReadOnlyList<CrateRef>>([])));
        _bus.Register<TagsByName, Task<IReadOnlyList<TagHit>>>(
            new F9QueryHandler<TagsByName, Task<IReadOnlyList<TagHit>>>(
                _ => Task.FromResult<IReadOnlyList<TagHit>>([])));
        _bus.Register<TopTags, Task<IReadOnlyList<TagHit>>>(
            new F9QueryHandler<TopTags, Task<IReadOnlyList<TagHit>>>(
                _ => Task.FromResult<IReadOnlyList<TagHit>>([])));
        _bus.Register<SearchTags, Task<IReadOnlyList<TagHit>>>(
            new F9QueryHandler<SearchTags, Task<IReadOnlyList<TagHit>>>(
                _ => Task.FromResult<IReadOnlyList<TagHit>>([])));
        // Opening the Glance overlay asks which deck to aim at and asks for a ranking.
        _bus.Register<SuggestLoadTarget, int>(new F9QueryHandler<SuggestLoadTarget, int>(_ => 0));
        _bus.Register<RankTracks, Task<RankedTracks>>(new F9QueryHandler<RankTracks, Task<RankedTracks>>(
            _ => Task.FromResult(new RankedTracks([], -1, null, null, false, []))));
        Record<SetSearchPick>();
    }

    private F9RecordingCommandHandler<T> Record<T>() where T : struct, ICommand
    {
        var handler = new F9RecordingCommandHandler<T>();
        _bus.Register<T>(handler);
        return handler;
    }

    private void ShowRows(params string[] titles) =>
        _bus.Publish(new LibraryRowsChanged(
            titles.Select(t => new TrackSummary($"/music/{t}.mp3", t, "Artist", TimeSpan.FromMinutes(3))
            {
                TrackId = Guid.NewGuid(),
            }).ToArray(),
            1));

    /// <summary>Put deck <paramref name="deck"/> in the loaded or empty state, the way the App announces it.</summary>
    private void SetLoaded(int deck, bool loaded) =>
        _bus.Publish(new DeckContentChanged(
            deck, null, loaded ? DeckLoadState.Loaded : DeckLoadState.Idle, loaded, null));

    private void SetPlaying(int deck, bool playing) =>
        _bus.Publish(new DeckPlayStateChanged(deck, playing ? PlayPhase.Playing : PlayPhase.Stopped, false));

    // ---- Controller guide ------------------------------------------------------------------------

    [Fact]
    public void Closing_the_guide_turns_inspect_off_at_the_start_of_the_collapse()
    {
        var overlay = new Sholto.Interface.Faceplate.ViewModels.FaceplateViewModel(
            new Sholto.Interface.Faceplate.Model.FaceplateDocLoader().Load(
                new Sholto.Interface.Faceplate.Devices.DdjFlx4.DdjFlx4Faceplate()), _bus);
        _vm.AttachFaceplate(overlay);
        var inspect = Record<SetInspectMode>();
        _vm.IsFaceplateOpen = true;
        Assert.Equal(CollapseToIconState.Open, _vm.FaceplateDock.State);

        _vm.IsFaceplateOpen = false;

        Assert.Equal(CollapseToIconState.Collapsing, _vm.FaceplateDock.State);
        Assert.Equal([true, false], inspect.Received.Select(c => c.On));
    }

    // ---- Selection ------------------------------------------------------------------------------

    [Fact]
    public void The_selection_follows_the_Apps_SelectionChanged_without_sending_anything()
    {
        var sent = Record<SelectTrack>();
        ShowRows("Alpha", "Bravo", "Charlie");

        _bus.Publish(new SelectionChanged(1, Guid.NewGuid()));

        Assert.Equal(1, _vm.SelectedTrackIndex);
        Assert.Equal("Bravo", _vm.SelectedTrackRow!.Title);
        Assert.Equal("Bravo", _vm.SelectedTrack!.Title);
        Assert.Contains(nameof(MainViewModel.SelectedTrackIndex), _changed);
        Assert.Contains(nameof(MainViewModel.SelectedTrack), _changed);
        Assert.Empty(sent.Received);
    }

    [Fact]
    public void With_nothing_highlighted_there_is_no_selected_row()
    {
        ShowRows("Alpha");

        Assert.Equal(-1, _vm.SelectedTrackIndex);
        Assert.Null(_vm.SelectedTrackRow);
        Assert.Null(_vm.SelectedTrack);
    }

    [Fact]
    public void A_selection_past_the_visible_rows_has_no_selected_row()
    {
        ShowRows("Alpha");

        _bus.Publish(new SelectionChanged(5, Guid.Empty));

        Assert.Null(_vm.SelectedTrackRow);
    }

    [Fact]
    public void Setting_the_selected_index_sends_SelectTrack_without_clamping()
    {
        var sent = Record<SelectTrack>();

        _vm.SelectedTrackIndex = 2;

        var command = Assert.Single(sent.Received);
        Assert.Equal(2, command.Index);
        Assert.False(command.Clamp);
        Assert.Equal(InterfaceIds.MainUI, command.Origin.InterfaceId);
        Assert.Equal(2, _vm.SelectedTrackIndex);
    }

    [Fact]
    public void Setting_the_selected_index_to_its_current_value_sends_nothing()
    {
        var sent = Record<SelectTrack>();
        _bus.Publish(new SelectionChanged(1, Guid.NewGuid()));

        _vm.SelectedTrackIndex = 1;

        Assert.Empty(sent.Received);
    }

    [Fact]
    public void Clearing_the_selection_with_minus_one_is_sent_as_given()
    {
        var sent = Record<SelectTrack>();
        _bus.Publish(new SelectionChanged(1, Guid.NewGuid()));

        _vm.SelectedTrackIndex = -1;

        var command = Assert.Single(sent.Received);
        Assert.Equal(-1, command.Index);
        Assert.False(command.Clamp);
    }

    [Fact]
    public void SelectTrack_asks_the_App_to_clamp()
    {
        var sent = Record<SelectTrack>();

        _vm.SelectTrack(7);

        var command = Assert.Single(sent.Received);
        Assert.Equal(7, command.Index);
        Assert.True(command.Clamp);
        Assert.Equal(InterfaceIds.MainUI, command.Origin.InterfaceId);
    }

    // ---- Crossfader -----------------------------------------------------------------------------

    [Fact]
    public void Setting_the_crossfader_sends_SetCrossfader_and_the_getter_waits_for_the_App()
    {
        var sent = Record<SetCrossfader>();

        _vm.Crossfader = 0.25;

        var command = Assert.Single(sent.Received);
        Assert.Equal(0.25, command.Position);
        Assert.Equal(InterfaceIds.MainUI, command.Origin.InterfaceId);
        Assert.Equal(0.5, _vm.Crossfader);

        _bus.Publish(new CrossfaderChanged(0.25));

        Assert.Equal(0.25, _vm.Crossfader);
        Assert.Contains(nameof(MainViewModel.Crossfader), _changed);
    }

    // ---- Escape and Delete ----------------------------------------------------------------------

    [Fact]
    public void HandleEscape_with_a_Track_List_loaded_returns_false_and_sends_nothing()
    {
        var removed = Record<RemoveSourceFromTrackList>();
        _bus.Publish(new TrackListChanged([new TrackListSource("crate:1", TrackListSourceKind.Crate, "Peak", 3)], 3));

        Assert.False(_vm.HandleEscape());

        Assert.Empty(removed.Received);
    }

    [Fact]
    public void HandleEscape_with_a_tuner_open_returns_true_and_closes_it()
    {
        var closed = Record<CloseTuneEditor>();
        _bus.Publish(new DeckEditChanged(0, true, false, false));
        Assert.True(_vm.Deck1.EditOpen);

        Assert.True(_vm.HandleEscape());

        Assert.Contains(closed.Received, c => c.Deck == 0);
    }

    [Fact]
    public void RemoveHighlighted_sends_the_path_of_the_highlighted_row()
    {
        var removed = Record<RemoveFromTrackList>();
        ShowRows("a", "b", "c", "d");
        _bus.Publish(new SelectionChanged(2, Guid.NewGuid()));

        _vm.RemoveHighlighted();

        var command = Assert.Single(removed.Received);
        Assert.Equal("/music/c.mp3", command.Path);
        Assert.Equal(InterfaceIds.MainUI, command.Origin.InterfaceId);
    }

    [Fact]
    public void RemoveHighlighted_with_no_highlight_sends_nothing()
    {
        var removed = Record<RemoveFromTrackList>();
        ShowRows("a", "b");
        _bus.Publish(new SelectionChanged(-1, Guid.Empty));

        _vm.RemoveHighlighted();

        Assert.Empty(removed.Received);
    }

    // ---- Unreachable folder, controller, magnet, harmony ----------------------------------------

    [Fact]
    public void The_unreachable_banner_follows_LibraryUnreachableChanged()
    {
        Assert.False(_vm.LibraryUnreachableVisible);

        _bus.Publish(new LibraryUnreachableChanged("/mnt/music"));

        Assert.Equal("/mnt/music", _vm.LibraryUnreachablePath);
        Assert.True(_vm.LibraryUnreachableVisible);
        Assert.Contains(nameof(MainViewModel.LibraryUnreachableVisible), _changed);

        _bus.Publish(new LibraryUnreachableChanged(null));

        Assert.Null(_vm.LibraryUnreachablePath);
        Assert.False(_vm.LibraryUnreachableVisible);
    }

    [Fact]
    public void The_controller_indicator_follows_DeviceConnectionChanged()
    {
        Assert.False(_vm.ControllerConnected);
        Assert.Equal("Reconnect USB", _vm.ControllerStatusText);

        _bus.Publish(new DeviceConnectionChanged(true));

        Assert.True(_vm.ControllerConnected);
        Assert.Equal("Controller", _vm.ControllerStatusText);
        Assert.Contains(nameof(MainViewModel.ControllerConnected), _changed);
        Assert.Contains(nameof(MainViewModel.ControllerStatusText), _changed);

        _bus.Publish(new DeviceConnectionChanged(false));

        Assert.False(_vm.ControllerConnected);
        Assert.Equal("Reconnect USB", _vm.ControllerStatusText);
    }

    [Fact]
    public void The_same_connection_state_again_notifies_nothing()
    {
        _bus.Publish(new DeviceConnectionChanged(true));
        _changed.Clear();

        _bus.Publish(new DeviceConnectionChanged(true));

        Assert.Empty(_changed);
    }

    [Fact]
    public void Magnet_eligibility_follows_MagnetEligibilityChanged()
    {
        Assert.False(_vm.IsMagnetEligible);

        _bus.Publish(new MagnetEligibilityChanged(true));

        Assert.True(_vm.IsMagnetEligible);
        Assert.Contains(nameof(MainViewModel.IsMagnetEligible), _changed);

        _bus.Publish(new MagnetEligibilityChanged(false));

        Assert.False(_vm.IsMagnetEligible);
    }

    [Fact]
    public void The_harmony_reference_key_follows_HarmonyReferenceChanged()
    {
        Assert.Null(_vm.HarmonyReferenceKey);

        _bus.Publish(new HarmonyReferenceChanged(new KeyRef(3, false), [new KeyRef(3, false)]));

        Assert.Equal(new KeyRef(3, false), _vm.HarmonyReferenceKey);
        Assert.Contains(nameof(MainViewModel.HarmonyReferenceKey), _changed);

        _bus.Publish(new HarmonyReferenceChanged(null, []));

        Assert.Null(_vm.HarmonyReferenceKey);
    }

    // ---- Toasts ---------------------------------------------------------------------------------

    [Fact]
    public void There_is_no_toast_to_begin_with()
    {
        Assert.Null(_vm.Toast);
        Assert.False(_vm.HasToast);
    }

    [Fact]
    public void A_marker_shows_a_toast_with_the_deck_and_time()
    {
        _bus.Publish(new MarkerAdded(0, 5.0));

        Assert.Equal("Marker · Deck 1 · 0:05", _vm.Toast);
        Assert.True(_vm.HasToast);
        Assert.Contains(nameof(MainViewModel.HasToast), _changed);
    }

    [Fact]
    public void A_marker_on_deck_2_past_a_minute_reads_minutes_and_seconds()
    {
        _bus.Publish(new MarkerAdded(1, 83.0));

        Assert.Equal("Marker · Deck 2 · 1:23", _vm.Toast);
    }

    [Fact]
    public void A_track_added_to_a_crate_shows_a_toast_with_the_crate_name()
    {
        _bus.Publish(new TrackAddedToCrate("Warmup", Guid.NewGuid()));

        Assert.Equal("Added to Warmup", _vm.Toast);
        Assert.True(_vm.HasToast);
    }

    [Fact]
    public void A_failed_load_shows_a_toast_with_the_title_and_deck()
    {
        _bus.Publish(new TrackLoadFailed(1, "/music/bad.mp3", "Bad Track"));

        Assert.Equal("Couldn't load Bad Track · Deck 2", _vm.Toast);
        Assert.True(_vm.HasToast);
    }

    // ---- Database overlays ----------------------------------------------------------------------

    [Fact]
    public void The_tag_editor_and_crate_picker_do_not_exist_before_the_database_is_attached()
    {
        Assert.Null(_vm.TagEditor);
        Assert.Null(_vm.CratePicker);
    }

    [Fact]
    public void An_attached_database_builds_the_tag_editor_and_the_crate_picker()
    {
        _bus.Publish(new LibraryDatabaseAttached(true));

        Assert.NotNull(_vm.TagEditor);
        Assert.NotNull(_vm.CratePicker);
        Assert.Contains(nameof(MainViewModel.TagEditor), _changed);
        Assert.Contains(nameof(MainViewModel.CratePicker), _changed);
    }

    [Fact]
    public async Task IsCratePickerOpen_follows_the_crate_picker_and_announces_each_change()
    {
        _bus.Publish(new LibraryDatabaseAttached(true));
        Assert.False(_vm.IsCratePickerOpen);
        _changed.Clear();

        _vm.CratePicker!.Close();
        Assert.False(_vm.IsCratePickerOpen);

        var row = new TrackRowFactory(new ThemeStackFactory().Build().Context).Create(
            new TrackSummary("/music/a.mp3", "Alpha", "Zed", TimeSpan.FromMinutes(3)) { TrackId = Guid.NewGuid() });
        await _vm.OpenCratePickerAsync(row);
        Assert.True(_vm.IsCratePickerOpen);
        Assert.Contains(nameof(MainViewModel.IsCratePickerOpen), _changed);

        _vm.CratePicker.Close();
        Assert.False(_vm.IsCratePickerOpen);
    }

    [Fact]
    public void Modals_skip_the_crate_picker_until_the_library_is_attached_and_list_it_first_after()
    {
        Assert.Equal<IModal>([_vm.SystemReportModal, _vm.LayoutWizard, _vm.Settings], _vm.Modals);

        _bus.Publish(new LibraryDatabaseAttached(true));

        Assert.Equal<IModal>([_vm.CratePicker!, _vm.SystemReportModal, _vm.LayoutWizard, _vm.Settings], _vm.Modals);
    }

    [Fact]
    public void Esc_with_two_modals_open_goes_to_the_higher_priority_one()
    {
        _vm.Settings.Open();
        _vm.SystemReportModal.Open();
        var router = new ModalKeyRouter();

        var first = _vm.Modals.First(m => m.IsOpen);
        Assert.Same(_vm.SystemReportModal, first);
        Assert.True(router.Route(first, Avalonia.Input.Key.Escape, Avalonia.Input.KeyModifiers.None));

        Assert.False(_vm.SystemReportModal.IsOpen);
        Assert.True(_vm.Settings.IsOpen);
    }

    [Fact]
    public void An_unavailable_database_builds_nothing()
    {
        _bus.Publish(new LibraryDatabaseAttached(false));

        Assert.Null(_vm.TagEditor);
        Assert.Null(_vm.CratePicker);
    }

    [Fact]
    public void A_second_attach_keeps_the_same_overlays()
    {
        _bus.Publish(new LibraryDatabaseAttached(true));
        var tagEditor = _vm.TagEditor;
        var cratePicker = _vm.CratePicker;

        _bus.Publish(new LibraryDatabaseAttached(true));

        Assert.Same(tagEditor, _vm.TagEditor);
        Assert.Same(cratePicker, _vm.CratePicker);
    }

    // ---- Search overlay -------------------------------------------------------------------------

    [Fact]
    public void IsSearchOpen_is_the_Glance_overlays_open_state_and_setting_it_opens_and_closes_it()
    {
        Assert.False(_vm.IsSearchOpen);

        _vm.IsSearchOpen = true;

        Assert.True(_vm.Glance.IsOpen);
        Assert.True(_vm.IsSearchOpen);
        Assert.Contains(nameof(MainViewModel.IsSearchOpen), _changed);

        _vm.IsSearchOpen = false;

        Assert.False(_vm.Glance.IsOpen);
    }

    [Fact]
    public void The_load_warning_shows_on_the_main_window_only_while_the_overlay_is_closed()
    {
        _bus.Publish(new LoadConfirmPending(true, 1, "Incoming", "Playing", 120));
        Assert.True(_vm.LoadFeedback.HasWarning);
        Assert.True(_vm.ShowLoadWarning);

        _vm.IsSearchOpen = true;

        Assert.True(_vm.LoadFeedback.HasWarning);
        Assert.False(_vm.ShowLoadWarning);
    }

    // ---- Load ------------------------------------------------------------------------------------

    [Fact]
    public void LoadSelectedToDeck_sends_LoadSelectedIntoDeck_for_that_deck()
    {
        var sent = Record<LoadSelectedIntoDeck>();

        _vm.LoadSelectedToDeck(1);

        var command = Assert.Single(sent.Received);
        Assert.Equal(1, command.Deck);
        Assert.Equal(InterfaceIds.MainUI, command.Origin.InterfaceId);
    }

    // ---- Decks ----------------------------------------------------------------------------------

    [Fact]
    public void The_deck_view_models_show_their_own_decks_events()
    {
        SetLoaded(1, true);

        Assert.False(_vm.Deck1.IsLoaded);
        Assert.True(_vm.Deck2.IsLoaded);
        Assert.Same(_vm.Deck1, _vm.DeckFor(0));
        Assert.Same(_vm.Deck2, _vm.DeckFor(1));
    }

    [Fact]
    public void The_editing_deck_is_the_one_whose_tune_editor_is_open_deck_1_first()
    {
        Assert.Null(_vm.EditingDeck);

        _bus.Publish(new DeckEditChanged(1, true, false, false));
        Assert.Same(_vm.Deck2, _vm.EditingDeck);

        _bus.Publish(new DeckEditChanged(0, true, false, false));
        Assert.Same(_vm.Deck1, _vm.EditingDeck);

        _bus.Publish(new DeckEditChanged(0, false, false, false));
        _bus.Publish(new DeckEditChanged(1, false, false, false));
        Assert.Null(_vm.EditingDeck);
    }

    // ---- System report (amber dot) ---------------------------------------------------------------

    private static SystemCheckReported MissingTool() =>
        new([new ToolStatus("demucs", ToolCapabilities.Stems, false, null, null)], SystemHealth.Degraded);

    [Fact]
    public void The_amber_dot_opens_the_system_report_and_IsSystemReportOpen_follows_it()
    {
        _bus.Publish(new DeviceConnectionChanged(true));
        _bus.Publish(MissingTool());

        _vm.OpenSystemReport();

        Assert.True(_vm.IsSystemReportOpen);
        Assert.True(_vm.SystemReportModal.IsOpen);
        Assert.Contains(nameof(MainViewModel.IsSystemReportOpen), _changed);
        _vm.CloseSystemReport();
        Assert.False(_vm.IsSystemReportOpen);
    }

    [Fact]
    public void A_published_system_check_sets_SystemDegraded_and_a_healthy_one_clears_it()
    {
        _bus.Publish(new DeviceConnectionChanged(true));
        Assert.False(_vm.SystemDegraded);
        _changed.Clear();

        _bus.Publish(MissingTool());
        Assert.True(_vm.SystemDegraded);
        Assert.Contains(nameof(MainViewModel.SystemDegraded), _changed);
        Assert.Single(_vm.SystemReportModal.Rows);

        _changed.Clear();
        _bus.Publish(new SystemCheckReported([], SystemHealth.Healthy));
        Assert.False(_vm.SystemDegraded);
        Assert.Contains(nameof(MainViewModel.SystemDegraded), _changed);
    }

    [Fact]
    public void A_healthy_or_red_dot_does_not_open_the_system_report()
    {
        _bus.Publish(new DeviceConnectionChanged(true));
        _bus.Publish(new SystemCheckReported([], SystemHealth.Healthy));
        _vm.OpenSystemReport();
        Assert.False(_vm.IsSystemReportOpen);

        _bus.Publish(MissingTool());
        _bus.Publish(new DeviceConnectionChanged(false));
        _vm.OpenSystemReport();
        Assert.False(_vm.IsSystemReportOpen);
    }

    // ---- Other actions --------------------------------------------------------------------------

    [Fact]
    public void Re_analyzing_the_selected_track_sends_ReanalyzeSelected()
    {
        var sent = Record<ReanalyzeSelected>();

        _vm.RequestReanalyzeSelected();

        var command = Assert.Single(sent.Received);
        Assert.Equal(InterfaceIds.MainUI, command.Origin.InterfaceId);
    }
}
