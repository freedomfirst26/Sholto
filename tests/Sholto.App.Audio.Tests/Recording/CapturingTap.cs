using Sholto.App.Audio.Recording;

namespace Sholto.App.Audio.Tests.Recording;

/// <summary>Keeps a copy of every buffer it is handed, with its channel count.</summary>
internal sealed class CapturingTap : IMasterTap
{
    public List<(float[] Buffer, int Channels)> Seen { get; } = [];

    public void Write(ReadOnlySpan<float> interleaved, int channels) => Seen.Add((interleaved.ToArray(), channels));
}
