namespace Sholto.Dsp;

/// <summary>RBJ Audio EQ Cookbook biquad, Direct Form I, single-channel.
/// Lives in the dependency-free DSP leaf because a signal-processing primitive
/// like this is neither analysis nor playback — it belongs to neither project
/// that uses it, so it belongs to both.</summary>
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

    public static Biquad LowPass(int sampleRate, float freq, float q)
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

    public static Biquad BandPass(int sampleRate, float freq, float q)
    {
        double w0 = 2 * Math.PI * freq / sampleRate;
        double cosW = Math.Cos(w0);
        double alpha = Math.Sin(w0) / (2 * q);
        double b0 = alpha;
        double b1 = 0;
        double b2 = -alpha;
        double a0 = 1 + alpha;
        double a1 = -2 * cosW;
        double a2 = 1 - alpha;
        return new Biquad
        {
            B0 = (float)(b0 / a0), B1 = (float)(b1 / a0), B2 = (float)(b2 / a0),
            A1 = (float)(a1 / a0), A2 = (float)(a2 / a0)
        };
    }

    public static Biquad HighPass(int sampleRate, float freq, float q)
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
