using Sholto.Bench.Rendering;

namespace Sholto.Bench.Tests;

/// <summary>
/// The WAV header/length arithmetic <see cref="WavWriter"/> patches in
/// <c>Dispose</c> — RIFF chunk size and data chunk size — read back byte-for-byte
/// with a plain <see cref="BinaryReader"/>, independent of any audio library.
/// </summary>
public sealed class WavWriterTests
{
    [Fact]
    public void Dispose_PatchesRiffAndDataChunkSizes_ToMatchWhatWasWritten()
    {
        string path = Path.Combine(Path.GetTempPath(), $"bench-wavwriter-{Guid.NewGuid():N}.wav");
        const int sampleRate = 48000;
        const int channels = 2;
        const int frames = 137; // arbitrary, not buffer-aligned — exercises real arithmetic

        try
        {
            var samples = new float[frames * channels];
            for (int i = 0; i < samples.Length; i++) samples[i] = i * 0.001f;

            using (var writer = new WavWriter(path, sampleRate, channels))
                writer.WriteInterleaved(samples);

            using var fs = File.OpenRead(path);
            using var br = new BinaryReader(fs);

            Assert.Equal("RIFF", new string(br.ReadChars(4)));
            int riffChunkSize = br.ReadInt32();
            Assert.Equal("WAVE", new string(br.ReadChars(4)));

            Assert.Equal("fmt ", new string(br.ReadChars(4)));
            int fmtChunkSize = br.ReadInt32();
            short audioFormat = br.ReadInt16();
            short fmtChannels = br.ReadInt16();
            int fmtSampleRate = br.ReadInt32();
            int byteRate = br.ReadInt32();
            short blockAlign = br.ReadInt16();
            short bitsPerSample = br.ReadInt16();

            Assert.Equal(16, fmtChunkSize);
            Assert.Equal((short)3, audioFormat); // WAVE_FORMAT_IEEE_FLOAT
            Assert.Equal((short)channels, fmtChannels);
            Assert.Equal(sampleRate, fmtSampleRate);
            Assert.Equal(sampleRate * channels * 4, byteRate);
            Assert.Equal((short)(channels * 4), blockAlign);
            Assert.Equal((short)32, bitsPerSample);

            Assert.Equal("data", new string(br.ReadChars(4)));
            int dataChunkSize = br.ReadInt32();

            int expectedDataBytes = frames * channels * 4;
            Assert.Equal(expectedDataBytes, dataChunkSize);

            long fileLength = new FileInfo(path).Length;
            Assert.Equal((int)(fileLength - 8), riffChunkSize);

            // Spot-check the actual sample payload round-trips, not just the header.
            for (int i = 0; i < samples.Length; i++)
                Assert.Equal(samples[i], br.ReadSingle(), precision: 6);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void WriteInterleaved_AcrossMultipleCalls_AccumulatesDataChunkSize()
    {
        string path = Path.Combine(Path.GetTempPath(), $"bench-wavwriter-{Guid.NewGuid():N}.wav");
        try
        {
            using (var writer = new WavWriter(path, 48000, channels: 1))
            {
                writer.WriteInterleaved(new float[10]);
                writer.WriteInterleaved(new float[5]);
                writer.WriteInterleaved(new float[1]);
            }

            using var fs = File.OpenRead(path);
            using var br = new BinaryReader(fs);
            fs.Position = 40; // "data" chunk size offset (see WavWriter's fixed 44-byte header layout)
            int dataChunkSize = br.ReadInt32();

            Assert.Equal(16 * 4, dataChunkSize); // (10+5+1) mono float32 samples
        }
        finally
        {
            File.Delete(path);
        }
    }
}
