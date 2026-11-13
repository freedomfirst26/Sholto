using Sholto.App.Audio.Recording;

namespace Sholto.App.Audio.Tests.Recording;

/// <summary>Keeps ch1-2 of every buffer it is handed, then passes the buffer on to <c>inner</c>.</summary>
internal sealed class TeeTap(IMasterTap inner) : IMasterTap
{
    private readonly IMasterTap _inner = inner;

    public List<float> Master { get; } = [];

    public void Write(ReadOnlySpan<float> interleaved, int channels)
    {
        for (int frame = 0; frame < interleaved.Length / channels; frame++)
        {
            Master.Add(interleaved[frame * channels]);
            Master.Add(interleaved[frame * channels + 1]);
        }
        _inner.Write(interleaved, channels);
    }
}
