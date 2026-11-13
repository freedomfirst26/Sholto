namespace Sholto.App.Recording;

/// <summary>The mix recording's start and stop, on the app thread.</summary>
public interface IMixRecording
{
    bool IsRecording { get; }

    /// <summary>Opens a new file and starts capturing the master into it; does nothing while recording.</summary>
    void Start();

    /// <summary>Stops capturing and finalises the file; does nothing while not recording.</summary>
    void Stop();
}
