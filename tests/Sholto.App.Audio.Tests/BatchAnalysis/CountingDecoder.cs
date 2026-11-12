using System.Collections.Concurrent;

namespace Sholto.App.Audio.Tests.BatchAnalysis;

/// <summary>An <see cref="IAudioFileDecoder"/> that returns a few silent frames and counts calls per path.</summary>
internal sealed class CountingDecoder : IAudioFileDecoder
{
    private readonly ConcurrentDictionary<string, int> _calls = new();

    public int CallsFor(string path) => _calls.GetValueOrDefault(path);

    public float[] Decode(string filePath)
    {
        _calls.AddOrUpdate(filePath, 1, (_, n) => n + 1);
        return new float[8];
    }
}
