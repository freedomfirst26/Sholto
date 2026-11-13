using Sholto.Data;
using Sholto.Interface.MainUI.ViewModels;

namespace Sholto.Interface.MainUI.Tests;

/// <summary>The record button: a click sends the toggle from the MainUI; on/off and the elapsed time come only
/// from the App's <see cref="MixRecordingChanged"/>.</summary>
public class MixRecordingViewModelTests
{
    private readonly DataBus _bus = new(new ThrowingFailureSink());
    private readonly F9RecordingCommandHandler<ToggleMixRecording> _toggles = new();
    private readonly MixRecordingViewModel _vm;

    public MixRecordingViewModelTests()
    {
        _bus.Register<ToggleMixRecording>(_toggles);
        _vm = new MixRecordingViewModel(_bus, _bus);
    }

    [Fact]
    public void Toggle_SendsToggleMixRecording()
    {
        _vm.Toggle();

        var sent = Assert.Single(_toggles.Received);
        Assert.Equal(new Origin(InterfaceIds.MainUI, "record-mix", "click"), sent.Origin);
        Assert.False(_vm.IsRecording);
    }

    [Fact]
    public void Changed_On_SetsIsRecordingAndElapsedText()
    {
        var changed = new List<string?>();
        _vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        _bus.Publish(new MixRecordingChanged(true, new TimeSpan(1, 2, 3), "/home/demo/Music/mix.wav"));

        Assert.True(_vm.IsRecording);
        Assert.Equal("1:02:03", _vm.ElapsedText);
        Assert.Contains(nameof(IMixRecordingViewModel.IsRecording), changed);
        Assert.Contains(nameof(IMixRecordingViewModel.ElapsedText), changed);
    }

    [Fact]
    public void Changed_Off_ClearsIsRecording()
    {
        _bus.Publish(new MixRecordingChanged(true, TimeSpan.FromSeconds(5), "/home/demo/Music/mix.wav"));

        _bus.Publish(new MixRecordingChanged(false, TimeSpan.FromSeconds(5), "/home/demo/Music/mix.wav"));

        Assert.False(_vm.IsRecording);
    }
}
