namespace Sholto.Interface.Bench.Tests.Rendering;

/// <summary>Reads back the 32-bit float stereo WAV that <c>WavWriter</c> produces (44-byte header).</summary>
public sealed class RenderedWav
{
    public int SampleRate { get; }
    public float[] Left { get; }

    public RenderedWav(string path)
    {
        var bytes = File.ReadAllBytes(path);
        SampleRate = BitConverter.ToInt32(bytes, 24);
        int channels = BitConverter.ToInt16(bytes, 22);
        int frames = (bytes.Length - 44) / (4 * channels);
        Left = new float[frames];
        for (int i = 0; i < frames; i++)
            Left[i] = BitConverter.ToSingle(bytes, 44 + i * 4 * channels);
    }
}
