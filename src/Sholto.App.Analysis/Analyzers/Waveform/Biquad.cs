namespace Sholto.App.Analysis.Analyzers.Waveform;

/// <summary>RBJ Audio EQ Cookbook biquad, Direct Form I, single-channel.
/// Sits beside <see cref="WaveformPeakAnalyzer"/>, its only consumer, which gets
/// coefficients from <see cref="BiquadFactory"/> to split the waveform into bands
/// before computing per-band peaks.</summary>
public struct Biquad
{
    public float B0, B1, B2, A1, A2;
    public float X1, X2, Y1, Y2;

    public float Process(float x)
    {
        float y = B0 * x + B1 * X1 + B2 * X2 - A1 * Y1 - A2 * Y2;
        X2 = X1; X1 = x;
        Y2 = Y1; Y1 = y;
        return y;
    }
}
