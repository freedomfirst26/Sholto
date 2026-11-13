namespace Sholto.App.Audio.Recording;

/// <summary>The producer side of a <see cref="IStereoSampleRing"/>: the audio thread.</summary>
public interface IStereoSampleWriter
{
    /// <summary>Copies ch1-2 of every frame of <paramref name="interleaved"/>, all or nothing.</summary>
    /// <returns>False, having copied nothing, when there is not room for every frame.</returns>
    bool TryWrite(ReadOnlySpan<float> interleaved, int channels);
}
