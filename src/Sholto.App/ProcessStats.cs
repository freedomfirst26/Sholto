using System.Diagnostics;

namespace Sholto.App;

/// <summary>
/// Process-self CPU% / working-set sampler. Two-shot sampling: each Sample() call
/// returns the CPU delta since the previous call, normalised to one core (so a
/// value of 100 means "one full logical core saturated", 800 on an 8-thread box
/// means "every core pegged"). Working set comes from Process.WorkingSet64.
///
/// Behind <c>SHOLTO_DEBUG_STATS=1</c> — App.axaml.cs only spins the polling timer
/// when <see cref="Enabled"/> is set, so this code is dormant in normal runs.
/// Composed once in App.axaml.cs, alongside the other bootstrap collaborators.
/// </summary>
public sealed class ProcessStats
{
    private readonly Process _self = Process.GetCurrentProcess();
    private TimeSpan _lastCpu = TimeSpan.Zero;
    private DateTime _lastSample = DateTime.UtcNow;
    private readonly int _cores = Math.Max(1, Environment.ProcessorCount);

    public bool Enabled =>
        Environment.GetEnvironmentVariable("SHOLTO_DEBUG_STATS") == "1";

    public (double cpuPercent, long workingSetBytes) Sample()
    {
        _self.Refresh();
        var now = DateTime.UtcNow;
        var cpuNow = _self.TotalProcessorTime;

        var elapsedMs = (now - _lastSample).TotalMilliseconds;
        var cpuUsedMs = (cpuNow - _lastCpu).TotalMilliseconds;

        _lastCpu = cpuNow;
        _lastSample = now;

        var cpu = elapsedMs > 0 ? (cpuUsedMs / elapsedMs) * 100.0 / _cores : 0;
        return (cpu, _self.WorkingSet64);
    }

    /// <summary>Pretty-printed one-liner like "CPU 24% · RAM 312 MB".</summary>
    public string SampleString()
    {
        var (cpu, mem) = Sample();
        return $"CPU {cpu,4:F0}% · RAM {mem / (1024.0 * 1024.0):F0} MB";
    }
}
