namespace Sholto.Interface.Bench.Tests.Rendering;

/// <summary>A deterministic mono-as-stereo test signal: a 200 Hz to 4 kHz chirp, so a delayed copy of
/// itself (what a running-sum bug adds) is not correlated with it. Zero mean, peak 0.5.</summary>
public sealed class SyntheticSource(int sampleRate, double seconds)
{
    public int SampleRate { get; } = sampleRate;
    public double Seconds { get; } = seconds;

    /// <summary>One channel of the signal, <c>SampleRate * Seconds</c> samples.</summary>
    public float[] Mono()
    {
        int n = (int)(SampleRate * Seconds);
        var mono = new float[n];
        const double f0 = 200, f1 = 4000;
        double k = (f1 - f0) / Seconds;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / SampleRate;
            mono[i] = (float)(0.5 * Math.Sin(2 * Math.PI * (f0 * t + 0.5 * k * t * t)));
        }
        return mono;
    }

    /// <summary>Writes the signal as a 16-bit stereo PCM WAV at <see cref="SampleRate"/>.</summary>
    public void WriteWav(string path)
    {
        var mono = Mono();
        using var bw = new BinaryWriter(File.Create(path));
        int dataBytes = mono.Length * 4;
        bw.Write("RIFF"u8); bw.Write(36 + dataBytes); bw.Write("WAVE"u8);
        bw.Write("fmt "u8); bw.Write(16); bw.Write((short)1); bw.Write((short)2);
        bw.Write(SampleRate); bw.Write(SampleRate * 4); bw.Write((short)4); bw.Write((short)16);
        bw.Write("data"u8); bw.Write(dataBytes);
        foreach (var s in mono)
        {
            short v = (short)Math.Round(s * 32767.0);
            bw.Write(v); bw.Write(v);
        }
    }
}
