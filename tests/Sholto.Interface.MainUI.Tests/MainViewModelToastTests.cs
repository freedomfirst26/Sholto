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

/// <summary>Toasts: the App's marker, crate and load-failure events shown as one toast line.</summary>
[Collection(MainViewModelCollection.Name)]
public class MainViewModelToastTests
{
    private readonly MainViewModelRig rig = new();

    [Fact]
    public void There_is_no_toast_to_begin_with()
    {
        Assert.Null(rig.Vm.Toast);
        Assert.False(rig.Vm.HasToast);
    }

    [Fact]
    public void A_marker_shows_a_toast_with_the_deck_and_time()
    {
        rig.Bus.Publish(new MarkerAdded(0, 5.0));

        Assert.Equal("Marker · Deck 1 · 0:05", rig.Vm.Toast);
        Assert.True(rig.Vm.HasToast);
        Assert.Contains(nameof(MainViewModel.HasToast), rig.Changed);
    }

    [Fact]
    public void A_marker_on_deck_2_past_a_minute_reads_minutes_and_seconds()
    {
        rig.Bus.Publish(new MarkerAdded(1, 83.0));

        Assert.Equal("Marker · Deck 2 · 1:23", rig.Vm.Toast);
    }

    [Fact]
    public void A_track_added_to_a_crate_shows_a_toast_with_the_crate_name()
    {
        rig.Bus.Publish(new TrackAddedToCrate("Warmup", Guid.NewGuid()));

        Assert.Equal("Added to Warmup", rig.Vm.Toast);
        Assert.True(rig.Vm.HasToast);
    }

    [Fact]
    public void A_failed_load_shows_a_toast_with_the_title_and_deck()
    {
        rig.Bus.Publish(new TrackLoadFailed(1, "/music/bad.mp3", "Bad Track"));

        Assert.Equal("Couldn't load Bad Track · Deck 2", rig.Vm.Toast);
        Assert.True(rig.Vm.HasToast);
    }

    [Fact]
    public void A_toast_announces_Toast_then_HasToast()
    {
        rig.Changed.Clear();

        rig.Bus.Publish(new MarkerAdded(0, 5.0));

        Assert.Equal([nameof(MainViewModel.Toast), nameof(MainViewModel.HasToast)], rig.Changed);
    }
}
