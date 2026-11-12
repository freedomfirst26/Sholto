namespace Sholto.App.Audio.Tests;

/// <summary>Answers with whatever backend the test sets, and counts how often it was asked.</summary>
internal sealed class FakeAudioBackendProbe(string? backend) : IAudioBackendProbe
{
    public string? Backend { get; set; } = backend;
    public int Reads { get; private set; }

    public string? ActiveBackend()
    {
        Reads++;
        return Backend;
    }
}
