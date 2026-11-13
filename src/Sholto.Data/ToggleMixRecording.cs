namespace Sholto.Data;

/// <summary>Start recording the master output to a WAV file, or stop the recording in progress.</summary>
public readonly record struct ToggleMixRecording(Origin Origin) : ICommand;
