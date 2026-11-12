using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;

using Sholto.App.Analysis.Stems;
using Sholto.Data;
using Sholto.TestSupport.Logging;
using Sholto.TestSupport.Platform;

namespace Sholto.App.ExternalTools.Tests;

public sealed class CudaProbingStemDeviceLogTests : IDisposable
{
    private const string Reason =
        "The probe failure is forced with a #! interpreter that lacks the Unix execute bit; Windows has no such bit.";

    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"sholto_probe_log_{Guid.NewGuid():N}");

    [LinuxOnlyFact(Reason)]
    [SupportedOSPlatform("linux")]
    public async Task A_failed_probe_logs_its_reason_at_error_under_the_demucs_tag()
    {
        Directory.CreateDirectory(_dir);
        var interpreter = Path.Combine(_dir, "python");
        File.WriteAllText(interpreter, "#!/bin/sh\necho True\n");
        File.SetUnixFileMode(interpreter, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        var demucs = Path.Combine(_dir, "demucs");
        File.WriteAllText(demucs, $"#!{interpreter}\n");
        var logs = new RecordingLogFactory();
        var device = new CudaProbingStemDevice(demucs, TimeSpan.FromSeconds(10), logs);

        Assert.Equal(StemDevice.Cpu, await device.ResolveAsync());

        var line = Assert.Single(logs.Lines);
        Assert.Equal(new LoggedLine("Demucs", LogLevel.Error, $"CUDA probe failed, using CPU: {StartFailureOf(interpreter)}"), line);
    }

    /// <summary>The message <see cref="Process.Start(ProcessStartInfo)"/> fails with for this interpreter.</summary>
    private string StartFailureOf(string interpreter)
    {
        var ex = Assert.Throws<Win32Exception>(() =>
            Process.Start(new ProcessStartInfo(interpreter) { UseShellExecute = false })?.Dispose());
        return ex.Message;
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }
}
