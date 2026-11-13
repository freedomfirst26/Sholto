using Sholto.App.Audio.Recording;

namespace Sholto.App.Audio.Tests.Recording;

/// <summary>Keeps every sample written; throws <see cref="ThrowOnWrite"/> instead when one is set.</summary>
internal sealed class FakeWavSink : IWavSink
{
    private readonly List<float> _samples = [];

    public Exception? ThrowOnWrite { get; init; }

    public int Flushes { get; private set; }

    public int Disposals { get; private set; }

    public IReadOnlyList<float> Samples => _samples;

    public void Write(ReadOnlySpan<float> stereo)
    {
        if (ThrowOnWrite is { } error) throw error;
        _samples.AddRange(stereo.ToArray());
    }

    public void Flush() => Flushes++;

    public void Dispose() => Disposals++;
}
