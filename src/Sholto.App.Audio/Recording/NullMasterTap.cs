namespace Sholto.App.Audio.Recording;

/// <summary>An <see cref="IMasterTap"/> that ignores every buffer: the router's tap when nothing records it (the
/// offline renders and their tests).</summary>
public sealed class NullMasterTap : IMasterTap
{
    public void Write(ReadOnlySpan<float> interleaved, int channels)
    {
    }
}
