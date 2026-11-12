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

/// <summary>The controller guide: attaching the faceplate and opening and closing it.</summary>
[Collection(MainViewModelCollection.Name)]
public class MainViewModelFaceplateGuideTests
{
    private readonly MainViewModelRig rig = new();

    [Fact]
    public void Closing_the_guide_turns_inspect_off_at_the_start_of_the_collapse()
    {
        var overlay = new Sholto.Interface.Faceplate.ViewModels.FaceplateViewModel(
            new Sholto.Interface.Faceplate.Model.FaceplateDocLoader().Load(
                new Sholto.Interface.Faceplate.Devices.DdjFlx4.DdjFlx4Faceplate()), rig.Bus);
        rig.Vm.AttachFaceplate(overlay);
        var inspect = rig.Record<SetInspectMode>();
        rig.Vm.IsFaceplateOpen = true;
        Assert.Equal(CollapseToIconState.Open, rig.Vm.FaceplateDock.State);

        rig.Vm.IsFaceplateOpen = false;

        Assert.Equal(CollapseToIconState.Collapsing, rig.Vm.FaceplateDock.State);
        Assert.Equal([true, false], inspect.Received.Select(c => c.On));
    }

    [Fact]
    public void Opening_and_closing_the_guide_announces_Mount_then_IsFaceplateOpen()
    {
        rig.Vm.AttachFaceplate(rig.NewFaceplate());
        string?[] expected = [nameof(MainViewModel.Mount), nameof(MainViewModel.IsFaceplateOpen)];
        rig.Changed.Clear();

        rig.Vm.IsFaceplateOpen = true;
        Assert.Equal(expected, rig.Changed);

        rig.Changed.Clear();
        rig.Vm.IsFaceplateOpen = false;
        Assert.Equal(expected, rig.Changed);
    }

    [Fact]
    public void Attaching_the_same_faceplate_twice_announces_once()
    {
        var faceplate = rig.NewFaceplate();
        rig.Changed.Clear();

        rig.Vm.AttachFaceplate(faceplate);
        Assert.Equal([nameof(MainViewModel.Faceplate)], rig.Changed);

        rig.Changed.Clear();
        rig.Vm.AttachFaceplate(faceplate);
        Assert.Empty(rig.Changed);
    }
}
