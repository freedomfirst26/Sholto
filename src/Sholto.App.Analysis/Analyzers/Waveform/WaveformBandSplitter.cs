namespace Sholto.App.Analysis.Analyzers.Waveform;

/// <summary>Default <see cref="IWaveformBandSplitter"/>: LP + HP biquads with mid
/// defined as the complement. Holds filter state — one instance per signal.</summary>
public sealed class WaveformBandSplitter(Biquad lowPass, Biquad highPass) : IWaveformBandSplitter
{
    private readonly Biquad _lowPass = lowPass;
    private readonly Biquad _highPass = highPass;

    public void Split(float mono, out float low, out float mid, out float high)
    {
        float lRaw = _lowPass.Process(mono);
        float hRaw = _highPass.Process(mono);
        // Complementary mid: everything the LP and HP didn't take. Phase
        // offsets from the filters smear this slightly, but abs+peak-track
        // washes that out — visually the three bands now cover 0..Nyquist
        // with no dead zone.
        low = MathF.Abs(lRaw);
        high = MathF.Abs(hRaw);
        mid = MathF.Abs(mono - lRaw - hRaw);
    }
}
