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
[Collection(MainViewModelCollection.Name)]
public class MainViewModelTests
{
    private readonly MainViewModelRig rig = new();

    // ---- Selection ------------------------------------------------------------------------------

    [Fact]
    public void The_selection_follows_the_Apps_SelectionChanged_without_sending_anything()
    {
        var sent = rig.Record<SelectTrack>();
        rig.ShowRows("Alpha", "Bravo", "Charlie");

        rig.Bus.Publish(new SelectionChanged(1, Guid.NewGuid()));

        Assert.Equal(1, rig.Vm.SelectedTrackIndex);
        Assert.Equal("Bravo", rig.Vm.SelectedTrackRow!.Title);
        Assert.Equal("Bravo", rig.Vm.SelectedTrack!.Title);
        Assert.Contains(nameof(MainViewModel.SelectedTrackIndex), rig.Changed);
        Assert.Contains(nameof(MainViewModel.SelectedTrack), rig.Changed);
        Assert.Empty(sent.Received);
    }

    [Fact]
    public void With_nothing_highlighted_there_is_no_selected_row()
    {
        rig.ShowRows("Alpha");

        Assert.Equal(-1, rig.Vm.SelectedTrackIndex);
        Assert.Null(rig.Vm.SelectedTrackRow);
        Assert.Null(rig.Vm.SelectedTrack);
    }

    [Fact]
    public void A_selection_past_the_visible_rows_has_no_selected_row()
    {
        rig.ShowRows("Alpha");

        rig.Bus.Publish(new SelectionChanged(5, Guid.Empty));

        Assert.Null(rig.Vm.SelectedTrackRow);
    }

    [Fact]
    public void Setting_the_selected_index_sends_SelectTrack_without_clamping()
    {
        var sent = rig.Record<SelectTrack>();

        rig.Vm.SelectedTrackIndex = 2;

        var command = Assert.Single(sent.Received);
        Assert.Equal(2, command.Index);
        Assert.False(command.Clamp);
        Assert.Equal(InterfaceIds.MainUI, command.Origin.InterfaceId);
        Assert.Equal(2, rig.Vm.SelectedTrackIndex);
    }

    [Fact]
    public void Setting_the_selected_index_to_its_current_value_sends_nothing()
    {
        var sent = rig.Record<SelectTrack>();
        rig.Bus.Publish(new SelectionChanged(1, Guid.NewGuid()));

        rig.Vm.SelectedTrackIndex = 1;

        Assert.Empty(sent.Received);
    }

    [Fact]
    public void Clearing_the_selection_with_minus_one_is_sent_as_given()
    {
        var sent = rig.Record<SelectTrack>();
        rig.Bus.Publish(new SelectionChanged(1, Guid.NewGuid()));

        rig.Vm.SelectedTrackIndex = -1;

        var command = Assert.Single(sent.Received);
        Assert.Equal(-1, command.Index);
        Assert.False(command.Clamp);
    }

    [Fact]
    public void SelectTrack_asks_the_App_to_clamp()
    {
        var sent = rig.Record<SelectTrack>();

        rig.Vm.SelectTrack(7);

        var command = Assert.Single(sent.Received);
        Assert.Equal(7, command.Index);
        Assert.True(command.Clamp);
        Assert.Equal(InterfaceIds.MainUI, command.Origin.InterfaceId);
    }

    // ---- Crossfader -----------------------------------------------------------------------------

    [Fact]
    public void Setting_the_crossfader_sends_SetCrossfader_and_the_getter_waits_for_the_App()
    {
        var sent = rig.Record<SetCrossfader>();

        rig.Vm.Crossfader = 0.25;

        var command = Assert.Single(sent.Received);
        Assert.Equal(0.25, command.Position);
        Assert.Equal(InterfaceIds.MainUI, command.Origin.InterfaceId);
        Assert.Equal(0.5, rig.Vm.Crossfader);

        rig.Bus.Publish(new CrossfaderChanged(0.25));

        Assert.Equal(0.25, rig.Vm.Crossfader);
        Assert.Contains(nameof(MainViewModel.Crossfader), rig.Changed);
    }

    // ---- Escape and Delete ----------------------------------------------------------------------

    [Fact]
    public void HandleEscape_with_a_Track_List_loaded_returns_false_and_sends_nothing()
    {
        var removed = rig.Record<RemoveSourceFromTrackList>();
        rig.Bus.Publish(new TrackListChanged([new TrackListSource("crate:1", TrackListSourceKind.Crate, "Peak", 3)], 3));

        Assert.False(rig.Vm.HandleEscape());

        Assert.Empty(removed.Received);
    }

    [Fact]
    public void HandleEscape_with_a_tuner_open_returns_true_and_closes_it()
    {
        var closed = rig.Record<CloseTuneEditor>();
        rig.Bus.Publish(new DeckEditChanged(0, true, false, false));
        Assert.True(rig.Vm.Deck1.EditOpen);

        Assert.True(rig.Vm.HandleEscape());

        Assert.Contains(closed.Received, c => c.Deck == 0);
    }

    [Fact]
    public void RemoveHighlighted_sends_the_path_of_the_highlighted_row()
    {
        var removed = rig.Record<RemoveFromTrackList>();
        rig.ShowRows("a", "b", "c", "d");
        rig.Bus.Publish(new SelectionChanged(2, Guid.NewGuid()));

        rig.Vm.RemoveHighlighted();

        var command = Assert.Single(removed.Received);
        Assert.Equal("/music/c.mp3", command.Path);
        Assert.Equal(InterfaceIds.MainUI, command.Origin.InterfaceId);
    }

    [Fact]
    public void RemoveHighlighted_with_no_highlight_sends_nothing()
    {
        var removed = rig.Record<RemoveFromTrackList>();
        rig.ShowRows("a", "b");
        rig.Bus.Publish(new SelectionChanged(-1, Guid.Empty));

        rig.Vm.RemoveHighlighted();

        Assert.Empty(removed.Received);
    }

    // ---- Unreachable folder, controller, magnet, harmony ----------------------------------------

    [Fact]
    public void The_unreachable_banner_follows_LibraryUnreachableChanged()
    {
        Assert.False(rig.Vm.LibraryUnreachableVisible);

        rig.Bus.Publish(new LibraryUnreachableChanged("/mnt/music"));

        Assert.Equal("/mnt/music", rig.Vm.LibraryUnreachablePath);
        Assert.True(rig.Vm.LibraryUnreachableVisible);
        Assert.Contains(nameof(MainViewModel.LibraryUnreachableVisible), rig.Changed);

        rig.Bus.Publish(new LibraryUnreachableChanged(null));

        Assert.Null(rig.Vm.LibraryUnreachablePath);
        Assert.False(rig.Vm.LibraryUnreachableVisible);
    }

    [Fact]
    public void The_controller_indicator_follows_DeviceConnectionChanged()
    {
        Assert.False(rig.Vm.ControllerConnected);
        Assert.Equal("Reconnect USB", rig.Vm.ControllerStatusText);

        rig.Bus.Publish(new DeviceConnectionChanged(true));

        Assert.True(rig.Vm.ControllerConnected);
        Assert.Equal("Controller", rig.Vm.ControllerStatusText);
        Assert.Contains(nameof(MainViewModel.ControllerConnected), rig.Changed);
        Assert.Contains(nameof(MainViewModel.ControllerStatusText), rig.Changed);

        rig.Bus.Publish(new DeviceConnectionChanged(false));

        Assert.False(rig.Vm.ControllerConnected);
        Assert.Equal("Reconnect USB", rig.Vm.ControllerStatusText);
    }

    [Fact]
    public void The_same_connection_state_again_notifies_nothing()
    {
        rig.Bus.Publish(new DeviceConnectionChanged(true));
        rig.Changed.Clear();

        rig.Bus.Publish(new DeviceConnectionChanged(true));

        Assert.Empty(rig.Changed);
    }

    [Fact]
    public void Magnet_eligibility_follows_MagnetEligibilityChanged()
    {
        Assert.False(rig.Vm.IsMagnetEligible);

        rig.Bus.Publish(new MagnetEligibilityChanged(true));

        Assert.True(rig.Vm.IsMagnetEligible);
        Assert.Contains(nameof(MainViewModel.IsMagnetEligible), rig.Changed);

        rig.Bus.Publish(new MagnetEligibilityChanged(false));

        Assert.False(rig.Vm.IsMagnetEligible);
    }

    [Fact]
    public void The_harmony_reference_key_follows_HarmonyReferenceChanged()
    {
        Assert.Null(rig.Vm.HarmonyReferenceKey);

        rig.Bus.Publish(new HarmonyReferenceChanged(new KeyRef(3, false), [new KeyRef(3, false)]));

        Assert.Equal(new KeyRef(3, false), rig.Vm.HarmonyReferenceKey);
        Assert.Contains(nameof(MainViewModel.HarmonyReferenceKey), rig.Changed);

        rig.Bus.Publish(new HarmonyReferenceChanged(null, []));

        Assert.Null(rig.Vm.HarmonyReferenceKey);
    }

    // ---- Search overlay -------------------------------------------------------------------------

    [Fact]
    public void IsSearchOpen_is_the_Glance_overlays_open_state_and_setting_it_opens_and_closes_it()
    {
        Assert.False(rig.Vm.IsSearchOpen);

        rig.Vm.IsSearchOpen = true;

        Assert.True(rig.Vm.Glance.IsOpen);
        Assert.True(rig.Vm.IsSearchOpen);
        Assert.Contains(nameof(MainViewModel.IsSearchOpen), rig.Changed);

        rig.Vm.IsSearchOpen = false;

        Assert.False(rig.Vm.Glance.IsOpen);
    }

    [Fact]
    public void The_load_warning_shows_on_the_main_window_only_while_the_overlay_is_closed()
    {
        rig.Bus.Publish(new LoadConfirmPending(true, 1, "Incoming", "Playing", 120));
        Assert.True(rig.Vm.LoadFeedback.HasWarning);
        Assert.True(rig.Vm.ShowLoadWarning);

        rig.Vm.IsSearchOpen = true;

        Assert.True(rig.Vm.LoadFeedback.HasWarning);
        Assert.False(rig.Vm.ShowLoadWarning);
    }

    // ---- Load ------------------------------------------------------------------------------------

    [Fact]
    public void LoadSelectedToDeck_sends_LoadSelectedIntoDeck_for_that_deck()
    {
        var sent = rig.Record<LoadSelectedIntoDeck>();

        rig.Vm.LoadSelectedToDeck(1);

        var command = Assert.Single(sent.Received);
        Assert.Equal(1, command.Deck);
        Assert.Equal(InterfaceIds.MainUI, command.Origin.InterfaceId);
    }

    // ---- Decks ----------------------------------------------------------------------------------

    [Fact]
    public void The_deck_view_models_show_their_own_decks_events()
    {
        rig.SetLoaded(1, true);

        Assert.False(rig.Vm.Deck1.IsLoaded);
        Assert.True(rig.Vm.Deck2.IsLoaded);
        Assert.Same(rig.Vm.Deck1, rig.Vm.DeckFor(0));
        Assert.Same(rig.Vm.Deck2, rig.Vm.DeckFor(1));
    }

    [Fact]
    public void The_editing_deck_is_the_one_whose_tune_editor_is_open_deck_1_first()
    {
        Assert.Null(rig.Vm.EditingDeck);

        rig.Bus.Publish(new DeckEditChanged(1, true, false, false));
        Assert.Same(rig.Vm.Deck2, rig.Vm.EditingDeck);

        rig.Bus.Publish(new DeckEditChanged(0, true, false, false));
        Assert.Same(rig.Vm.Deck1, rig.Vm.EditingDeck);

        rig.Bus.Publish(new DeckEditChanged(0, false, false, false));
        rig.Bus.Publish(new DeckEditChanged(1, false, false, false));
        Assert.Null(rig.Vm.EditingDeck);
    }

    // ---- Other actions --------------------------------------------------------------------------

    [Fact]
    public void Re_analyzing_the_selected_track_sends_ReanalyzeSelected()
    {
        var sent = rig.Record<ReanalyzeSelected>();

        rig.Vm.RequestReanalyzeSelected();

        var command = Assert.Single(sent.Received);
        Assert.Equal(InterfaceIds.MainUI, command.Origin.InterfaceId);
    }

    [Fact]
    public async Task Output_picker_is_open_while_a_question_is_pending()
    {
        Assert.False(rig.Vm.IsOutputPickerOpen);

        var ask = rig.Vm.OutputPicker.AskAsync([new OutputDeviceChoice("Speakers", true)], null);
        Assert.True(rig.Vm.IsOutputPickerOpen);

        rig.Vm.OutputPicker.Commit();
        Assert.False(rig.Vm.IsOutputPickerOpen);
        Assert.Equal("Speakers", await ask);
    }

    [Fact]
    public void The_output_picker_announces_IsOutputPickerOpen_on_open_and_close()
    {
        rig.Changed.Clear();

        _ = rig.Vm.OutputPicker.AskAsync([new OutputDeviceChoice("Speakers", true)], null);
        Assert.Equal([nameof(MainViewModel.IsOutputPickerOpen)], rig.Changed);

        rig.Changed.Clear();
        rig.Vm.OutputPicker.Cancel();
        Assert.Equal([nameof(MainViewModel.IsOutputPickerOpen)], rig.Changed);
    }
}
