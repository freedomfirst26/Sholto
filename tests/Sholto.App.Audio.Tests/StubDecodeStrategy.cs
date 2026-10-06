namespace Sholto.App.Audio.Tests;

/// <summary>Test strategy that claims .mp3, counts calls and returns a marker
/// buffer, or throws when constructed with a failure.</summary>
public sealed class StubDecodeStrategy(float marker, bool fails = false) : IAudioDecodeStrategy
{
    private readonly float _marker = marker;
    private readonly bool _fails = fails;

    public int Calls { get; private set; }

    public bool CanDecode(string extension) => extension == ".mp3";

    public float[] Decode(string filePath)
    {
        Calls++;
        if (_fails) throw new InvalidOperationException("stub failure");
        return [_marker];
    }
}
