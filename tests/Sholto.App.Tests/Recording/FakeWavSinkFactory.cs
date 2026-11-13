using Sholto.App.Audio.Recording;

namespace Sholto.App.Tests.Recording;

/// <summary>Opens nothing: every sink is a <see cref="NullWavSink"/> it keeps.</summary>
internal sealed class FakeWavSinkFactory : IWavSinkFactory
{
    public List<NullWavSink> Opened { get; } = [];

    public IWavSink Create(string path)
    {
        var sink = new NullWavSink();
        Opened.Add(sink);
        return sink;
    }
}
