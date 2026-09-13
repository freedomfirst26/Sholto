namespace Sholto.Audio.Golden;

/// <summary>
/// Reads back exactly what <see cref="Sholto.Bench.Rendering.WavWriter"/>
/// writes: a 44-byte canonical header (RIFF/WAVE, one "fmt " chunk, one
/// "data" chunk) with 32-bit IEEE float samples. Not a general WAV parser —
/// deliberately as small and dumb as the writer it mirrors.
/// </summary>
internal static class WavFloatReader
{
    public static (int sampleRate, int channels, float[] samples) Read(string path)
    {
        using var fs = File.OpenRead(path);
        using var br = new BinaryReader(fs);

        string riff = new(br.ReadChars(4));
        if (riff != "RIFF") throw new FormatException($"{path}: not a RIFF file");
        br.ReadInt32(); // riff chunk size, unused
        string wave = new(br.ReadChars(4));
        if (wave != "WAVE") throw new FormatException($"{path}: not a WAVE file");

        string fmt = new(br.ReadChars(4));
        if (fmt != "fmt ") throw new FormatException($"{path}: expected 'fmt ' chunk, found '{fmt}'");
        int fmtSize = br.ReadInt32();
        short audioFormat = br.ReadInt16();
        short channels = br.ReadInt16();
        int sampleRate = br.ReadInt32();
        br.ReadInt32();   // byte rate
        br.ReadInt16();   // block align
        short bitsPerSample = br.ReadInt16();
        if (fmtSize > 16) br.ReadBytes(fmtSize - 16); // skip any extension

        if (audioFormat != 3 || bitsPerSample != 32)
            throw new FormatException($"{path}: expected 32-bit IEEE float WAV, found format={audioFormat} bits={bitsPerSample}");

        string data = new(br.ReadChars(4));
        if (data != "data") throw new FormatException($"{path}: expected 'data' chunk, found '{data}'");
        int dataBytes = br.ReadInt32();

        var samples = new float[dataBytes / 4];
        for (int i = 0; i < samples.Length; i++) samples[i] = br.ReadSingle();

        return (sampleRate, channels, samples);
    }
}
