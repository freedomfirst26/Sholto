using Xunit;
using OpenDJ.Library;

namespace OpenDJ.Library.Tests;

public class TrackScannerTests
{
    [Fact]
    public async Task Scan_EmptyDirectory_ReturnsEmpty()
    {
        var dir = Directory.CreateTempSubdirectory("opendj_test_");
        try
        {
            var tracks = await TrackScanner.ScanAsync(dir.FullName);
            Assert.Empty(tracks);
        }
        finally { dir.Delete(true); }
    }

    [Fact]
    public async Task Scan_DirectoryWithWavFile_ReturnsOneTrack()
    {
        var dir = Directory.CreateTempSubdirectory("opendj_test_");
        try
        {
            var wavPath = Path.Combine(dir.FullName, "test.wav");
            WriteMinimalWav(wavPath);

            var tracks = await TrackScanner.ScanAsync(dir.FullName);
            Assert.Single(tracks);
            Assert.Equal(Path.GetFullPath(wavPath), tracks[0].FilePath);
        }
        finally { dir.Delete(true); }
    }

    [Fact]
    public async Task Scan_NonExistentDirectory_ReturnsEmpty()
    {
        var tracks = await TrackScanner.ScanAsync("/tmp/opendj_does_not_exist_xyz");
        Assert.Empty(tracks);
    }

    [Fact]
    public async Task Scan_IgnoresNonAudioFiles()
    {
        var dir = Directory.CreateTempSubdirectory("opendj_test_");
        try
        {
            File.WriteAllText(Path.Combine(dir.FullName, "readme.txt"), "hello");
            File.WriteAllText(Path.Combine(dir.FullName, "image.jpg"), "fake");

            var tracks = await TrackScanner.ScanAsync(dir.FullName);
            Assert.Empty(tracks);
        }
        finally { dir.Delete(true); }
    }

    private static void WriteMinimalWav(string path)
    {
        using var writer = new BinaryWriter(File.Create(path));
        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)2);
        writer.Write(44100);
        writer.Write(176400);
        writer.Write((short)4);
        writer.Write((short)16);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        writer.Write(0);
    }
}
