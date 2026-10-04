using Sholto.App.Analysis.Analyzers.Segments;
using Sholto.App.Analysis.Analyzers.Waveform;
using Sholto.App.Analysis.Harmony;
using Sholto.App.Audio;
using Sholto.App.Library;
using Sholto.Data;
using Sholto.Interface.MainUI.Models;
using Sholto.Interface.MainUI.Theming;
using Sholto.Interface.MainUI.ViewModels;
using DeckContent = Sholto.Data.DeckContentChanged<
    Sholto.App.Library.Track, Sholto.App.Audio.TrackAnalysis, Sholto.App.Analysis.Analyzers.Segments.SongSegment>;

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
        var search = new SearchViewModel(rows.Items, new LibrarySearch(), recency, _bus, _bus, appThread);
        _vm = new MainViewModel(
            themes.Context,
            themes.Catalog,
            new OverlayViewModelFactory(recency, _bus, _bus, _bus, appThread),
            rows,
            _bus,
            _bus,
            appThread,
            search,
            new TrackActionsViewModel(),
            new DeckViewModel(0, _bus, _bus, themes.Context, new WaveformPeaksFactory()),
            new DeckViewModel(1, _bus, _bus, themes.Context, new WaveformPeaksFactory()));
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
        _bus.Publish(new DeckContent(
            deck, null, loaded ? DeckLoadState.Loaded : DeckLoadState.Idle, loaded, null, null));

    private void SetPlaying(int deck, bool playing) =>
        _bus.Publish(new DeckPlayStateChanged(deck, playing ? PlayPhase.Playing : PlayPhase.Stopped, false));

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

    // ---- Filter ---------------------------------------------------------------------------------

    [Fact]
    public void The_active_filter_follows_LibraryFilterChanged()
    {
        _bus.Publish(new LibraryFilterChanged("peak"));

        Assert.Equal("peak", _vm.ActiveFilter);
        Assert.Contains(nameof(MainViewModel.ActiveFilter), _changed);

        _bus.Publish(new LibraryFilterChanged(null));

        Assert.Null(_vm.ActiveFilter);
    }

    [Fact]
    public void ClearFilter_sends_ClearLibraryFilter()
    {
        var sent = Record<ClearLibraryFilter>();

        _vm.ClearFilter();

        var command = Assert.Single(sent.Received);
        Assert.Equal(InterfaceIds.MainUI, command.Origin.InterfaceId);
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

        _bus.Publish(new HarmonyReferenceChanged(new KeyRef(3, false)));

        Assert.Equal(new Key(3, false), _vm.HarmonyReferenceKey);
        Assert.Contains(nameof(MainViewModel.HarmonyReferenceKey), _changed);

        _bus.Publish(new HarmonyReferenceChanged(null));

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
    public void Picking_a_tag_in_the_search_overlay_filters_by_it_and_closes_the_overlay()
    {
        var sent = Record<FilterLibraryByTag>();
        _vm.IsSearchOpen = true;

        _vm.Search.PickTag("peak");

        var command = Assert.Single(sent.Received);
        Assert.Equal("peak", command.Tag);
        Assert.Equal(InterfaceIds.MainUI, command.Origin.InterfaceId);
        Assert.False(_vm.IsSearchOpen);
    }

    [Fact]
    public void Picking_a_crate_in_the_search_overlay_filters_by_it_and_closes_the_overlay()
    {
        var sent = Record<FilterLibraryByCrate>();
        _vm.IsSearchOpen = true;

        _vm.Search.PickCrate(new CrateRef(7, "Warmup", 3));

        var command = Assert.Single(sent.Received);
        Assert.Equal(7, command.CrateId);
        Assert.Equal("Warmup", command.Name);
        Assert.Equal(InterfaceIds.MainUI, command.Origin.InterfaceId);
        Assert.False(_vm.IsSearchOpen);
    }

    // ---- Load target ----------------------------------------------------------------------------

    [Fact]
    public void LoadSelectedToDeck_sends_LoadSelectedIntoDeck_for_that_deck()
    {
        var sent = Record<LoadSelectedIntoDeck>();

        _vm.LoadSelectedToDeck(1);

        var command = Assert.Single(sent.Received);
        Assert.Equal(1, command.Deck);
        Assert.Equal(InterfaceIds.MainUI, command.Origin.InterfaceId);
    }

    [Fact]
    public void With_both_decks_empty_the_load_target_is_deck_1()
    {
        _vm.LoadTargetDeck = 1;

        _vm.PickDefaultLoadTarget();

        Assert.Equal(0, _vm.LoadTargetDeck);
        Assert.True(_vm.IsLoadTargetDeck1);
        Assert.False(_vm.IsLoadTargetDeck2);
    }

    [Fact]
    public void The_empty_deck_is_the_load_target_when_only_deck_1_is_loaded()
    {
        SetLoaded(0, true);
        SetLoaded(1, false);

        _vm.PickDefaultLoadTarget();

        Assert.Equal(1, _vm.LoadTargetDeck);
        Assert.True(_vm.IsLoadTargetDeck2);
    }

    [Fact]
    public void The_empty_deck_is_the_load_target_when_only_deck_2_is_loaded()
    {
        _vm.LoadTargetDeck = 1;
        SetLoaded(0, false);
        SetLoaded(1, true);

        _vm.PickDefaultLoadTarget();

        Assert.Equal(0, _vm.LoadTargetDeck);
    }

    [Fact]
    public void With_both_loaded_the_load_target_is_the_deck_that_is_not_playing()
    {
        SetLoaded(0, true);
        SetLoaded(1, true);
        SetPlaying(0, true);

        _vm.PickDefaultLoadTarget();
        Assert.Equal(1, _vm.LoadTargetDeck);

        SetPlaying(0, false);
        SetPlaying(1, true);

        _vm.PickDefaultLoadTarget();
        Assert.Equal(0, _vm.LoadTargetDeck);
    }

    [Fact]
    public void With_both_loaded_and_both_or_neither_playing_the_load_target_is_deck_1()
    {
        _vm.LoadTargetDeck = 1;
        SetLoaded(0, true);
        SetLoaded(1, true);

        _vm.PickDefaultLoadTarget();
        Assert.Equal(0, _vm.LoadTargetDeck);

        _vm.LoadTargetDeck = 1;
        SetPlaying(0, true);
        SetPlaying(1, true);

        _vm.PickDefaultLoadTarget();
        Assert.Equal(0, _vm.LoadTargetDeck);
    }

    [Fact]
    public void Opening_the_search_overlay_picks_the_default_load_target()
    {
        SetLoaded(0, true);
        SetLoaded(1, false);

        _vm.IsSearchOpen = true;

        Assert.Equal(1, _vm.LoadTargetDeck);
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
