using Sholto.App.Audio;

namespace Sholto.App.Tests;

/// <summary>Decodes nothing: returns a fixed buffer, or throws, and remembers what it was asked to decode.</summary>
internal sealed class FakeAudioFileDecoder(Exception? failure = null) : IAudioFileDecoder
{
    private readonly Exception? _failure = failure;
    private readonly List<string> _decoded = [];

    public IReadOnlyList<string> Decoded
    {
        get { lock (_decoded) return [.. _decoded]; }
    }

    public float[] Decode(string filePath)
    {
        lock (_decoded) _decoded.Add(filePath);
        if (_failure is not null) throw _failure;
        return [0f, 0f, 0f, 0f];
    }
}
