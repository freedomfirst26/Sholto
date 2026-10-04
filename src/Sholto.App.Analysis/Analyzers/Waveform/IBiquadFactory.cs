namespace Sholto.App.Analysis.Analyzers.Waveform;

/// <summary>Designs <see cref="Biquad"/> coefficients for the waveform band split.</summary>
public interface IBiquadFactory
{
    Biquad LowPass(int sampleRate, float freq, float q);
    Biquad HighPass(int sampleRate, float freq, float q);
}
