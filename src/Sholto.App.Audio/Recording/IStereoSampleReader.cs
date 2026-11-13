namespace Sholto.App.Audio.Recording;

/// <summary>The consumer side of a <see cref="IStereoSampleRing"/>: the recording's writer thread.</summary>
public interface IStereoSampleReader
{
    /// <summary>Moves as many whole stereo frames as are waiting and fit into <paramref name="stereoDest"/>.</summary>
    /// <returns>The floats read, a multiple of 2.</returns>
    int Read(Span<float> stereoDest);

    /// <summary>Frames written and not yet read.</summary>
    int AvailableFrames { get; }
}
