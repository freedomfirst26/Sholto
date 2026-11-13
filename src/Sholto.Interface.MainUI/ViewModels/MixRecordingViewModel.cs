using System.ComponentModel;
using System.Runtime.CompilerServices;
using Sholto.Data;

namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>The record button: a click sends <see cref="ToggleMixRecording"/>; whether it is recording and for how
/// long come only from the App's <see cref="MixRecordingChanged"/>, replayed on subscribe.</summary>
public sealed class MixRecordingViewModel : IMixRecordingViewModel, IEventHandler<MixRecordingChanged>
{
    private const string ControlId = "record-mix";
    private const string Gesture = "click";
    private const string ElapsedFormat = @"h\:mm\:ss";

    private readonly ICommandSender _sender;
    private bool _isRecording;
    private TimeSpan _elapsed;

    public MixRecordingViewModel(ICommandSender sender, IEventSubscriber subscriber)
    {
        _sender = sender;
        subscriber.Subscribe<MixRecordingChanged>(this);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsRecording => _isRecording;

    public string ElapsedText => _elapsed.ToString(ElapsedFormat);

    public void Toggle() => _sender.Send(new ToggleMixRecording(new Origin(InterfaceIds.MainUI, ControlId, Gesture)));

    void IEventHandler<MixRecordingChanged>.Handle(in MixRecordingChanged e)
    {
        _isRecording = e.On;
        _elapsed = e.Elapsed;
        Notify(nameof(IsRecording));
        Notify(nameof(ElapsedText));
    }

    private void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
