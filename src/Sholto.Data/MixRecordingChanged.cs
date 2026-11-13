namespace Sholto.Data;

/// <summary>Whether the mix is being recorded, for how long, and to which file. State: published at start, once per
/// whole second of captured audio, and at stop; a late subscriber is told the current state.</summary>
/// <param name="On">True while recording.</param>
/// <param name="Elapsed">Captured audio so far, in whole seconds; 0 at the start of each recording.</param>
/// <param name="FilePath">The WAV file being written, or last written.</param>
public readonly record struct MixRecordingChanged(bool On, TimeSpan Elapsed, string? FilePath) : IStateEvent
{
    public int Slot => 0;
}
