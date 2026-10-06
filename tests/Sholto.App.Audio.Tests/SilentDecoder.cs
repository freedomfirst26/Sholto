namespace Sholto.App.Audio.Tests;

/// <summary>An <see cref="IAudioFileDecoder"/> that returns a few silent frames.</summary>
internal sealed class SilentDecoder : IAudioFileDecoder
{
    public float[] Decode(string filePath) => new float[8];
}
