namespace Sholto.App.Audio.Recording;

/// <summary>An open WAV file that takes interleaved stereo float frames. Disposing it finalises the header.</summary>
public interface IWavSink : IDisposable
{
    void Write(ReadOnlySpan<float> stereo);

    /// <summary>Rewrites the header for what is written so far, so a crash leaves a playable file.</summary>
    void Flush();
}
