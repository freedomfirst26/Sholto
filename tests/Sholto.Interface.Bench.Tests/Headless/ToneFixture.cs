namespace Sholto.Interface.Bench.Tests.Headless;

/// <summary>Writes a 48 kHz 16-bit stereo sine WAV, the engine's native rate, so the streaming load
/// path is taken (no resample) in a headless run.</summary>
public sealed class ToneFixture(double frequencyHz, double seconds)
{
    public const int SampleRate = 48000;

    public void WriteWav(string path)
    {
        int n = (int)(SampleRate * seconds);
        using var bw = new BinaryWriter(File.Create(path));
        int dataBytes = n * 4;
        bw.Write("RIFF"u8); bw.Write(36 + dataBytes); bw.Write("WAVE"u8);
        bw.Write("fmt "u8); bw.Write(16); bw.Write((short)1); bw.Write((short)2);
        bw.Write(SampleRate); bw.Write(SampleRate * 4); bw.Write((short)4); bw.Write((short)16);
        bw.Write("data"u8); bw.Write(dataBytes);
        for (int i = 0; i < n; i++)
        {
            short v = (short)Math.Round(0.5 * Math.Sin(2 * Math.PI * frequencyHz * i / SampleRate) * 32767.0);
            bw.Write(v); bw.Write(v);
        }
    }
}
