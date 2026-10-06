namespace Sholto.Data;

/// <summary>The waveform style the user chose last time, read from settings once the library database is
/// open. A fact: not replayed. An interface that draws waveforms applies it if it still knows that id.</summary>
public readonly record struct SavedWaveformStyleFound(string Id) : IEvent;
