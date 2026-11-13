namespace Sholto.App.Audio.Recording;

/// <summary>Drains a recording's ring into its WAV file on a background thread of its own.</summary>
public interface IMixRecordingWriter
{
    /// <summary>Starts the background thread.</summary>
    void Start();

    /// <summary>Asks the thread to write what is left in the ring and exit, waits up to <paramref name="timeout"/>
    /// for it, then closes the file on the calling thread.</summary>
    /// <returns>False when the thread did not finish in time; the file is then left as it is.</returns>
    bool StopAndWait(TimeSpan timeout);

    /// <summary>Why writing failed, once it has; the thread has then closed the file and exited. Null until then.</summary>
    string? FaultReason { get; }
}
