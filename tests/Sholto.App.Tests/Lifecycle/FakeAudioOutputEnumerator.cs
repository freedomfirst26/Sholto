using Sholto.App.Audio;

namespace Sholto.App.Tests;

/// <summary>Lists the devices it was given and counts how often it was asked.</summary>
internal sealed class FakeAudioOutputEnumerator(params AudioDevice[] devices) : IAudioOutputEnumerator
{
    private readonly AudioDevice[] _devices = devices;
    private int _calls;

    public int Calls => Volatile.Read(ref _calls);

    public IReadOnlyList<AudioDevice> EnumerateOutputs()
    {
        Interlocked.Increment(ref _calls);
        return _devices;
    }
}
