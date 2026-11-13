using Sholto.Data;
using Sholto.Interface.MainUI.ViewModels;

namespace Sholto.Interface.MainUI.Tests;

/// <summary>The end of a recording attempt shows exactly one toast: the saved file, why it stopped early (and
/// what was saved), or why it never started.</summary>
[Collection(MainViewModelCollection.Name)]
public class MixRecordingToastTests
{
    private const string Path = "/home/demo/Music/2026-11-12 21-04-33.wav";
    private const string FileName = "2026-11-12 21-04-33.wav";

    private readonly MainViewModelRig rig = new();

    [Fact]
    public void Stopped_Normal_ShowsMixSaved()
    {
        rig.Bus.Publish(new MixRecordingChanged(false, TimeSpan.FromSeconds(30), Path));
        rig.Bus.Publish(new MixRecordingStopped(Path, null));

        Assert.Equal($"Mix saved: {FileName}", rig.Vm.Toast);
        Assert.Single(rig.Changed, nameof(MainViewModel.Toast));
    }

    [Fact]
    public void Stopped_WithReason_ShowsOnlyFailureToast()
    {
        rig.Bus.Publish(new MixRecordingChanged(false, TimeSpan.FromSeconds(30), Path));
        rig.Bus.Publish(new MixRecordingStopped(Path, "disk full"));

        Assert.StartsWith("Recording stopped: disk full", rig.Vm.Toast);
        Assert.Equal($"Recording stopped: disk full (saved {FileName})", rig.Vm.Toast);
        Assert.Single(rig.Changed, nameof(MainViewModel.Toast));
    }

    [Fact]
    public void Stopped_NeverStarted_ShowsCouldntStart()
    {
        rig.Bus.Publish(new MixRecordingStopped(null, "the folder is read-only"));

        Assert.Equal("Couldn't start recording: the folder is read-only", rig.Vm.Toast);
        Assert.Single(rig.Changed, nameof(MainViewModel.Toast));
    }
}
