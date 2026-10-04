namespace Sholto.App.Analysis.Analyzers.Waveform;

/// <summary>Creates a fresh <see cref="IWaveformBandSplitter"/> (new filter state) per signal.</summary>
public interface IWaveformBandSplitterFactory
{
    IWaveformBandSplitter Create(int sampleRate);
}
