namespace Sholto.App.Audio.Recording;

/// <summary>Sees every finished device buffer the output router composed. Called on the audio thread only, once per
/// buffer, so an implementation must not allocate, lock, block or do I/O.</summary>
public interface IMasterTap
{
    /// <param name="interleaved">The device buffer: master on ch1-2, the headphone cue on ch3-4 when present.</param>
    /// <param name="channels">The device's channel count, the stride of <paramref name="interleaved"/>.</param>
    void Write(ReadOnlySpan<float> interleaved, int channels);
}
