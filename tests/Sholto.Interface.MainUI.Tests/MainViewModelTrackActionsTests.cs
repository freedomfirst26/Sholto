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

/// <summary>Track actions: the database overlays (tag editor, crate picker) and the modal list.</summary>
[Collection(MainViewModelCollection.Name)]
public class MainViewModelTrackActionsTests
{
    private readonly MainViewModelRig rig = new();

    [Fact]
    public void The_tag_editor_and_crate_picker_do_not_exist_before_the_database_is_attached()
    {
        Assert.Null(rig.Vm.TagEditor);
        Assert.Null(rig.Vm.CratePicker);
    }

    [Fact]
    public void An_attached_database_builds_the_tag_editor_and_the_crate_picker()
    {
        rig.Bus.Publish(new LibraryDatabaseAttached(true));

        Assert.NotNull(rig.Vm.TagEditor);
        Assert.NotNull(rig.Vm.CratePicker);
        Assert.Contains(nameof(MainViewModel.TagEditor), rig.Changed);
        Assert.Contains(nameof(MainViewModel.CratePicker), rig.Changed);
    }

    [Fact]
    public async Task IsCratePickerOpen_follows_the_crate_picker_and_announces_each_change()
    {
        rig.Bus.Publish(new LibraryDatabaseAttached(true));
        Assert.False(rig.Vm.IsCratePickerOpen);
        rig.Changed.Clear();

        rig.Vm.CratePicker!.Close();
        Assert.False(rig.Vm.IsCratePickerOpen);

        var row = new TrackRowFactory(new ThemeStackFactory().Build().Context).Create(
            new TrackSummary("/music/a.mp3", "Alpha", "Zed", TimeSpan.FromMinutes(3)) { TrackId = Guid.NewGuid() });
        await rig.Vm.OpenCratePickerAsync(row);
        Assert.True(rig.Vm.IsCratePickerOpen);
        Assert.Contains(nameof(MainViewModel.IsCratePickerOpen), rig.Changed);

        rig.Vm.CratePicker.Close();
        Assert.False(rig.Vm.IsCratePickerOpen);
    }

    [Fact]
    public void Modals_skip_the_crate_picker_until_the_library_is_attached_and_list_it_first_after()
    {
        Assert.Equal<IModal>([rig.Vm.SystemReportModal, rig.Vm.LayoutWizard, rig.Vm.Settings, rig.Vm.Folders], rig.Vm.Modals);

        rig.Bus.Publish(new LibraryDatabaseAttached(true));

        Assert.Equal<IModal>([rig.Vm.CratePicker!, rig.Vm.SystemReportModal, rig.Vm.LayoutWizard, rig.Vm.Settings, rig.Vm.Folders], rig.Vm.Modals);
    }

    [Fact]
    public void Esc_with_two_modals_open_goes_to_the_higher_priority_one()
    {
        rig.Vm.Settings.Open();
        rig.Vm.SystemReportModal.Open();
        var router = new ModalKeyRouter();

        var first = rig.Vm.Modals.First(m => m.IsOpen);
        Assert.Same(rig.Vm.SystemReportModal, first);
        Assert.True(router.Route(first, Avalonia.Input.Key.Escape, Avalonia.Input.KeyModifiers.None));

        Assert.False(rig.Vm.SystemReportModal.IsOpen);
        Assert.True(rig.Vm.Settings.IsOpen);
    }

    [Fact]
    public void An_unavailable_database_builds_nothing()
    {
        rig.Bus.Publish(new LibraryDatabaseAttached(false));

        Assert.Null(rig.Vm.TagEditor);
        Assert.Null(rig.Vm.CratePicker);
    }

    [Fact]
    public void A_second_attach_keeps_the_same_overlays()
    {
        rig.Bus.Publish(new LibraryDatabaseAttached(true));
        var tagEditor = rig.Vm.TagEditor;
        var cratePicker = rig.Vm.CratePicker;

        rig.Bus.Publish(new LibraryDatabaseAttached(true));

        Assert.Same(tagEditor, rig.Vm.TagEditor);
        Assert.Same(cratePicker, rig.Vm.CratePicker);
    }

    [Fact]
    public void An_attached_database_announces_TagEditor_then_CratePicker()
    {
        rig.Changed.Clear();

        rig.Bus.Publish(new LibraryDatabaseAttached(true));

        Assert.Equal([nameof(MainViewModel.TagEditor), nameof(MainViewModel.CratePicker)], rig.Changed);
    }

    [Fact]
    public void Opening_track_actions_announces_IsTrackActionsOpen()
    {
        rig.ShowRows("Alpha");
        rig.Changed.Clear();

        rig.Vm.OpenTrackActions(rig.Vm.Tracks[0]);
        Assert.Equal([nameof(MainViewModel.IsTrackActionsOpen)], rig.Changed);

        rig.Changed.Clear();
        rig.Vm.TrackActions.Close();
        Assert.Equal([nameof(MainViewModel.IsTrackActionsOpen)], rig.Changed);
    }
}
