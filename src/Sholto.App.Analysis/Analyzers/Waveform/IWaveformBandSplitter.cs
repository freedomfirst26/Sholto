namespace Sholto.App.Analysis.Analyzers.Waveform;

/// <summary>Splits one mono sample into low/mid/high band magnitudes. Stateful
/// (the underlying filters carry history), so use one instance per signal.</summary>
public interface IWaveformBandSplitter
{
    /// <summary>Feeds one mono sample; returns the absolute value of each band.</summary>
    void Split(float mono, out float low, out float mid, out float high);
}
