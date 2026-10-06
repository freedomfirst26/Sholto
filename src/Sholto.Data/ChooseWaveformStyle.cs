namespace Sholto.Data;

/// <summary>The user picked a waveform style (by its stable id, e.g. "three-band", "rgb"); the App
/// remembers it for the next launch. The style itself is presentation: the interface that draws
/// waveforms owns what the id means.</summary>
public readonly record struct ChooseWaveformStyle(string Id, Origin Origin) : ICommand
{
    /// <summary>Not a deck command.</summary>
    public int Deck => -1;
}
