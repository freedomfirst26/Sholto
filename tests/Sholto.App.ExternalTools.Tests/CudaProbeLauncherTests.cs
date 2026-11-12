using Sholto.App.Analysis.Stems;

namespace Sholto.App.ExternalTools.Tests;

/// <summary>demucs as a launcher binary with no <c>#!</c> line (uv/pip's Windows <c>demucs.exe</c>):
/// the probe asks the python beside it, in the same folder or the parent folder.</summary>
public class CudaProbeLauncherTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"sholto_launcher_{Guid.NewGuid():N}");
    private readonly string _scripts;
    private readonly string _demucs;

    public CudaProbeLauncherTests()
    {
        _scripts = Path.Combine(_root, "Scripts");
        Directory.CreateDirectory(_scripts);
        _demucs = Path.Combine(_scripts, "demucs.exe");
        // A PE launcher starts "MZ" and is binary; no #! line.
        File.WriteAllBytes(_demucs, [(byte)'M', (byte)'Z', 0x90, 0x00, 0x03, 0x00, 0xFF, 0xFF, (byte)'\n']);
    }

    [Theory]
    [InlineData("python.exe")]
    [InlineData("python3")]
    [InlineData("python")]
    public async Task Probe_asks_the_python_in_the_launchers_own_folder(string name)
    {
        FakePython(_scripts, name, "True");

        Assert.Equal(StemDevice.Cuda, await Device().ResolveAsync());
    }

    [Fact]
    public async Task Probe_asks_the_python_in_the_launchers_parent_folder()
    {
        FakePython(_root, "python.exe", "True");

        Assert.Equal(StemDevice.Cuda, await Device().ResolveAsync());
    }

    [Fact]
    public async Task The_python_in_the_launchers_own_folder_wins_over_the_parent()
    {
        FakePython(_scripts, "python.exe", "False");
        FakePython(_root, "python.exe", "True");

        Assert.Equal(StemDevice.Cpu, await Device().ResolveAsync());
    }

    [Fact]
    public async Task Probe_reports_cpu_when_no_python_is_beside_the_launcher()
    {
        Assert.Equal(StemDevice.Cpu, await Device().ResolveAsync());
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private CudaProbingStemDevice Device() => new(_demucs, TimeSpan.FromSeconds(10), new RecordingLogFactory());

    private void FakePython(string dir, string name, string answer)
    {
        var path = Path.Combine(dir, name);
        File.WriteAllText(path, $"#!/bin/sh\necho {answer}\n");
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }
}
