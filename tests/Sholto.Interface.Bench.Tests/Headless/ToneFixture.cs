namespace Sholto.Interface.Bench.Tests.Headless;

/// <summary>Writes a 16-bit stereo sine WAV. At the default 48 kHz, the engine's native rate, the streaming
/// load path is taken (no resample) in a headless run; at any other rate the track is decoded whole into
/// the in-memory reader a platter scratch needs.</summary>
public sealed class ToneFixture(double frequencyHz, double seconds, int sampleRate = ToneFixture.SampleRate)
{
    public const int SampleRate = 48000;

    private readonly double _frequencyHz = frequencyHz;
    private readonly double _seconds = seconds;
    private readonly int _sampleRate = sampleRate;

    public void WriteWav(string path)
    {
        int n = (int)(_sampleRate * _seconds);
        using var bw = new BinaryWriter(File.Create(path));
        int dataBytes = n * 4;
        bw.Write("RIFF"u8); bw.Write(36 + dataBytes); bw.Write("WAVE"u8);
        bw.Write("fmt "u8); bw.Write(16); bw.Write((short)1); bw.Write((short)2);
        bw.Write(_sampleRate); bw.Write(_sampleRate * 4); bw.Write((short)4); bw.Write((short)16);
        bw.Write("data"u8); bw.Write(dataBytes);
        for (int i = 0; i < n; i++)
        {
            short v = (short)Math.Round(0.5 * Math.Sin(2 * Math.PI * _frequencyHz * i / _sampleRate) * 32767.0);
            bw.Write(v); bw.Write(v);
        }
    }
}
