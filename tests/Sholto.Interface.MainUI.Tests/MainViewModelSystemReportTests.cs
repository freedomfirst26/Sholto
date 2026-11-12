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

/// <summary>The system report: the amber dot, its modal and the degraded flag.</summary>
[Collection(MainViewModelCollection.Name)]
public class MainViewModelSystemReportTests
{
    private readonly MainViewModelRig rig = new();

    [Fact]
    public void The_amber_dot_opens_the_system_report_and_IsSystemReportOpen_follows_it()
    {
        rig.Bus.Publish(new DeviceConnectionChanged(true));
        rig.Bus.Publish(rig.MissingTool());

        rig.Vm.OpenSystemReport();

        Assert.True(rig.Vm.IsSystemReportOpen);
        Assert.True(rig.Vm.SystemReportModal.IsOpen);
        Assert.Contains(nameof(MainViewModel.IsSystemReportOpen), rig.Changed);
        rig.Vm.CloseSystemReport();
        Assert.False(rig.Vm.IsSystemReportOpen);
    }

    [Fact]
    public void A_published_system_check_sets_SystemDegraded_and_a_healthy_one_clears_it()
    {
        rig.Bus.Publish(new DeviceConnectionChanged(true));
        Assert.False(rig.Vm.SystemDegraded);
        rig.Changed.Clear();

        rig.Bus.Publish(rig.MissingTool());
        Assert.True(rig.Vm.SystemDegraded);
        Assert.Contains(nameof(MainViewModel.SystemDegraded), rig.Changed);
        Assert.Single(rig.Vm.SystemReportModal.Rows);

        rig.Changed.Clear();
        rig.Bus.Publish(new SystemCheckReported([], SystemHealth.Healthy));
        Assert.False(rig.Vm.SystemDegraded);
        Assert.Contains(nameof(MainViewModel.SystemDegraded), rig.Changed);
    }

    [Fact]
    public void A_healthy_or_red_dot_does_not_open_the_system_report()
    {
        rig.Bus.Publish(new DeviceConnectionChanged(true));
        rig.Bus.Publish(new SystemCheckReported([], SystemHealth.Healthy));
        rig.Vm.OpenSystemReport();
        Assert.False(rig.Vm.IsSystemReportOpen);

        rig.Bus.Publish(rig.MissingTool());
        rig.Bus.Publish(new DeviceConnectionChanged(false));
        rig.Vm.OpenSystemReport();
        Assert.False(rig.Vm.IsSystemReportOpen);
    }

    [Fact]
    public void Opening_the_system_report_announces_IsSystemReportOpen()
    {
        rig.Bus.Publish(new DeviceConnectionChanged(true));
        rig.Bus.Publish(rig.MissingTool());
        rig.Changed.Clear();

        rig.Vm.OpenSystemReport();

        Assert.Equal([nameof(MainViewModel.IsSystemReportOpen)], rig.Changed);
    }
}
