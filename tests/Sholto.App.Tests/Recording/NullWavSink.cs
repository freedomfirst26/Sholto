using Sholto.App.Audio.Recording;

namespace Sholto.App.Tests.Recording;

/// <summary>Discards what it is written; counts its disposals.</summary>
internal sealed class NullWavSink : IWavSink
{
    public int Disposals { get; private set; }

    public void Write(ReadOnlySpan<float> stereo)
    {
    }

    public void Flush()
    {
    }

    public void Dispose() => Disposals++;
}
