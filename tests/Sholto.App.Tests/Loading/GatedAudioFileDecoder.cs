using Sholto.App.Audio;

namespace Sholto.App.Tests;

/// <summary>A decoder whose decode of each file blocks until the test releases that file, so a test chooses the
/// order in which decodes complete.</summary>
internal sealed class GatedAudioFileDecoder : IAudioFileDecoder
{
    private readonly Dictionary<string, ManualResetEventSlim> _gates = [];

    public float[] Decode(string filePath)
    {
        GateFor(filePath).Wait(TimeSpan.FromSeconds(10));
        return [0f, 0f, 0f, 0f];
    }

    /// <summary>Let the decode of <paramref name="filePath"/> finish (now, or when it starts).</summary>
    public void Release(string filePath) => GateFor(filePath).Set();

    private ManualResetEventSlim GateFor(string filePath)
    {
        lock (_gates)
        {
            if (!_gates.TryGetValue(filePath, out var gate)) _gates[filePath] = gate = new ManualResetEventSlim(false);
            return gate;
        }
    }
}
