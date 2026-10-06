namespace Sholto.App.ExternalTools.Tests;

/// <summary>A demucs entry script whose <c>#!</c> line names a fake interpreter that prints
/// <paramref name="answer"/> and counts its runs, so <see cref="CudaProbingStemDevice"/> runs for real
/// without torch.</summary>
internal sealed class ProbeRig : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"sholto_probe_{Guid.NewGuid():N}");
    private readonly string _counter;

    public CudaProbingStemDevice Device { get; }
    public int Runs => File.Exists(_counter) ? File.ReadAllLines(_counter).Length : 0;

    public ProbeRig(string answer)
    {
        Directory.CreateDirectory(_dir);
        _counter = Path.Combine(_dir, "runs");
        var interpreter = Path.Combine(_dir, "python");
        File.WriteAllText(interpreter, $"#!/bin/sh\necho run >> '{_counter}'\necho {answer}\n");
        var demucs = Path.Combine(_dir, "demucs");
        File.WriteAllText(demucs, $"#!{interpreter}\n");
        File.SetUnixFileMode(interpreter, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        Device = new CudaProbingStemDevice(demucs, TimeSpan.FromSeconds(10));
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);
}
