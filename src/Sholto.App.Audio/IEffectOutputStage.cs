namespace Sholto.App.Audio;

/// <summary>
/// The wet/dry Mix blend and the cross-thread reset fade that wrap an effect's DSP.
/// <see cref="SetMix"/> and <see cref="RequestReset"/> may be called from any thread;
/// <see cref="Process"/> runs on the audio thread only.
/// </summary>
internal interface IEffectOutputStage
{
    /// <summary>Set the Mix target, 0..1. NaN is ignored.</summary>
    void SetMix(double value);

    /// <summary>Ask the audio thread to fade out, reset the DSP and fade back in.</summary>
    void RequestReset();

    /// <summary>Run <paramref name="dsp"/> over the buffer with the Mix blend and reset fade applied.</summary>
    void Process(Span<float> buffer, int channels, IEffectDsp dsp);
}
