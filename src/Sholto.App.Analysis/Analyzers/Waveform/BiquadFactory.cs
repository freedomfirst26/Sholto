namespace Sholto.App.Analysis.Analyzers.Waveform;

/// <summary>Designs <see cref="Biquad"/> coefficients (RBJ Audio EQ Cookbook) for
/// <see cref="WaveformPeakAnalyzer"/>'s band split. Holds no state.</summary>
public sealed class BiquadFactory : IBiquadFactory
{
    public Biquad LowPass(int sampleRate, float freq, float q)
    {
        double w0 = 2 * Math.PI * freq / sampleRate;
        double cosW = Math.Cos(w0);
        double alpha = Math.Sin(w0) / (2 * q);
        double b0 = (1 - cosW) / 2;
        double b1 = 1 - cosW;
        double b2 = (1 - cosW) / 2;
        double a0 = 1 + alpha;
        double a1 = -2 * cosW;
        double a2 = 1 - alpha;
        return new Biquad
        {
            B0 = (float)(b0 / a0), B1 = (float)(b1 / a0), B2 = (float)(b2 / a0),
            A1 = (float)(a1 / a0), A2 = (float)(a2 / a0)
        };
    }

    public Biquad HighPass(int sampleRate, float freq, float q)
    {
        double w0 = 2 * Math.PI * freq / sampleRate;
        double cosW = Math.Cos(w0);
        double alpha = Math.Sin(w0) / (2 * q);
        double b0 = (1 + cosW) / 2;
        double b1 = -(1 + cosW);
        double b2 = (1 + cosW) / 2;
        double a0 = 1 + alpha;
        double a1 = -2 * cosW;
        double a2 = 1 - alpha;
        return new Biquad
        {
            B0 = (float)(b0 / a0), B1 = (float)(b1 / a0), B2 = (float)(b2 / a0),
            A1 = (float)(a1 / a0), A2 = (float)(a2 / a0)
        };
    }
}
