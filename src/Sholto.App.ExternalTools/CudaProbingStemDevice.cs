using System.Diagnostics;

using Sholto.App.Analysis.Stems;

namespace Sholto.App.ExternalTools;

/// <summary>
/// Confirms once whether demucs will find CUDA, by asking torch in demucs's own interpreter
/// (the <c>#!</c> line of the demucs entry script), and caches the answer for the life of the
/// process. Anything short of a clean "True" — no script, no interpreter, a crash, a timeout —
/// is <see cref="StemDevice.Cpu"/>, so a machine without CUDA keeps the serial order.
/// The probe runs on the thread pool; it is not tied to any caller's cancellation, because
/// its answer is shared by every later caller.
/// </summary>
public sealed class CudaProbingStemDevice : IStemDevice
{
    private const string ProbeScript = "import torch;print(torch.cuda.is_available())";

    private readonly Lazy<Task<StemDevice>> _device;

    public CudaProbingStemDevice(string demucsBinaryPath, TimeSpan timeout)
    {
        _device = new Lazy<Task<StemDevice>>(
            () => Task.Run(() => Probe(demucsBinaryPath, timeout)), LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public Task<StemDevice> ResolveAsync(CancellationToken ct = default) => _device.Value.WaitAsync(ct);

    private StemDevice Probe(string demucsBinaryPath, TimeSpan timeout)
    {
        try
        {
            var interpreter = InterpreterOf(demucsBinaryPath);
            if (interpreter is null) return StemDevice.Cpu;

            var psi = new ProcessStartInfo(interpreter)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            psi.ArgumentList.Add("-c");
            psi.ArgumentList.Add(ProbeScript);

            using var process = Process.Start(psi);
            if (process is null) return StemDevice.Cpu;
            var stdout = process.StandardOutput.ReadToEndAsync();
            process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(timeout))
            {
                process.Kill(entireProcessTree: true);
                return StemDevice.Cpu;
            }
            return process.ExitCode == 0 && stdout.GetAwaiter().GetResult().Trim() == "True"
                ? StemDevice.Cuda
                : StemDevice.Cpu;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Demucs] CUDA probe failed, using CPU: {ex.Message}");
            return StemDevice.Cpu;
        }
    }

    /// <summary>The interpreter named on the script's <c>#!</c> line, or null if the file is not such a script.</summary>
    private string? InterpreterOf(string demucsBinaryPath)
    {
        if (!File.Exists(demucsBinaryPath)) return null;
        using var reader = new StreamReader(demucsBinaryPath);
        var first = reader.ReadLine();
        if (first is null || !first.StartsWith("#!", StringComparison.Ordinal)) return null;
        var interpreter = first[2..].Trim().Split(' ', 2)[0];
        // "#!/usr/bin/env python" would need its argument handled; treat it as unconfirmed.
        return Path.GetFileName(interpreter) != "env" && File.Exists(interpreter) ? interpreter : null;
    }
}
