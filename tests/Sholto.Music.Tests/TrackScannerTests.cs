using System.Diagnostics;
using Xunit;
using Sholto.Music;

namespace Sholto.Music.Tests;

public class TrackScannerTests
{
    [Fact]
    public async Task Scan_DirectoryWithM4aFile_ReturnsOneTrackWithTitle()
    {
        if (!IsFfmpegAvailable())
        {
            // ffmpeg generates the test fixture (and is what decodes M4A at runtime);
            // skip on a machine that doesn't have it rather than fail.
            return;
        }

        var dir = Directory.CreateTempSubdirectory("sholto_test_");
        try
        {
            var m4aPath = Path.Combine(dir.FullName, "test.m4a");
            CreateSineM4aFixture(m4aPath);

            var tracks = await TrackScanner.ScanAsync(dir.FullName);

            Assert.Single(tracks);
            Assert.False(string.IsNullOrWhiteSpace(tracks[0].Title));
        }
        finally { dir.Delete(true); }
    }

    private static bool IsFfmpegAvailable()
    {
        try
        {
            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName = "ffmpeg",
                ArgumentList = { "-version" },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            });
            proc!.WaitForExit(5000);
            return proc.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static void CreateSineM4aFixture(string path)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add("-y");
        psi.ArgumentList.Add("-f");
        psi.ArgumentList.Add("lavfi");
        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add("sine=frequency=440:sample_rate=44100:duration=2");
        psi.ArgumentList.Add("-c:a");
        psi.ArgumentList.Add("aac");
        psi.ArgumentList.Add("-b:a");
        psi.ArgumentList.Add("128k");
        psi.ArgumentList.Add(path);

        using var proc = Process.Start(psi)!;
        proc.WaitForExit();
        if (proc.ExitCode != 0)
        {
            string stderr = proc.StandardError.ReadToEnd();
            throw new InvalidOperationException($"Failed to generate test fixture: {stderr}");
        }
    }

    [Fact]
    public async Task Scan_EmptyDirectory_ReturnsEmpty()
    {
        var dir = Directory.CreateTempSubdirectory("sholto_test_");
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
        var dir = Directory.CreateTempSubdirectory("sholto_test_");
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
        var tracks = await TrackScanner.ScanAsync("/tmp/sholto_does_not_exist_xyz");
        Assert.Empty(tracks);
    }

    [Fact]
    public async Task Scan_IgnoresNonAudioFiles()
    {
        var dir = Directory.CreateTempSubdirectory("sholto_test_");
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
