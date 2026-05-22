using System.Diagnostics;

namespace Sholto.App;

/// <summary>
/// Process-self CPU% / working-set sampler. Two-shot sampling: each Sample() call
/// returns the CPU delta since the previous call, normalised to one core (so a
/// value of 100 means "one full logical core saturated", 800 on an 8-thread box
/// means "every core pegged"). Working set comes from Process.WorkingSet64.
///
/// Behind <c>SHOLTO_DEBUG_STATS=1</c> — App.axaml.cs only spins the polling timer
/// when the env var is set, so this code is dormant in normal runs.
/// </summary>
public static class ProcessStats
{
    private static readonly Process Self = Process.GetCurrentProcess();
    private static TimeSpan _lastCpu = TimeSpan.Zero;
    private static DateTime _lastSample = DateTime.UtcNow;
    private static readonly int Cores = Math.Max(1, Environment.ProcessorCount);

    // Visible whenever any debug instrumentation is active. Right now that
    // means "loop-output WAV recording is on" — when you ask for that, you
    // probably want to see whether the recorder is impacting CPU / RAM too.
    public static bool Enabled =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENDJ_LOOP_DEBUG_WAV"))
        || Environment.GetEnvironmentVariable("SHOLTO_DEBUG_STATS") == "1";

    public static (double cpuPercent, long workingSetBytes) Sample()
    {
        Self.Refresh();
        var now = DateTime.UtcNow;
        var cpuNow = Self.TotalProcessorTime;

        var elapsedMs = (now - _lastSample).TotalMilliseconds;
        var cpuUsedMs = (cpuNow - _lastCpu).TotalMilliseconds;

        _lastCpu = cpuNow;
        _lastSample = now;

        var cpu = elapsedMs > 0 ? (cpuUsedMs / elapsedMs) * 100.0 / Cores : 0;
        return (cpu, Self.WorkingSet64);
    }

    /// <summary>Pretty-printed one-liner like "CPU 24% · RAM 312 MB".</summary>
    public static string SampleString()
    {
        var (cpu, mem) = Sample();
        return $"CPU {cpu,4:F0}% · RAM {mem / (1024.0 * 1024.0):F0} MB";
    }
}
