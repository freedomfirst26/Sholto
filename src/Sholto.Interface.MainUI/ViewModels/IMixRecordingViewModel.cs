using System.ComponentModel;

namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>The top bar's record button and elapsed-time label.</summary>
public interface IMixRecordingViewModel : INotifyPropertyChanged
{
    /// <summary>The mix is being recorded (the App's <c>MixRecordingChanged.On</c>).</summary>
    bool IsRecording { get; }

    /// <summary>Recorded so far, as H:MM:SS (e.g. "1:02:03").</summary>
    string ElapsedText { get; }

    /// <summary>Start recording, or stop the recording in progress.</summary>
    void Toggle();
}
