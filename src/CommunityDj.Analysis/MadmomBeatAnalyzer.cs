using System.Diagnostics;
using System.Globalization;

namespace CommunityDj.Analysis;

/// <summary>
/// Beat / downbeat detection — shells out to madmom's DBNDownBeatTracker
/// (RNN + dynamic Bayesian network). Required, no fallback.
///
/// Install once:
///   sudo apt install ffmpeg
///   uv tool install madmom-onnx
/// </summary>
public static class MadmomBeatAnalyzer
{
    /// <summary>Where DBNDownBeatTracker lives on this machine, or null if not installed.</summary>
    public static string? BinaryPath { get; } = FindBinary("DBNDownBeatTracker");

    public static bool IsAvailable => BinaryPath is not null;

    /// <summary>Run madmom on the file and return (bpm, beat times, downbeat times).</summary>
    public static async Task<(double Bpm, double[] BeatTimes, double[] DownbeatTimes)> AnalyzeAsync(
        string filePath, CancellationToken ct = default)
    {
        if (BinaryPath is null)
            throw new InvalidOperationException(
                "madmom DBNDownBeatTracker not found. Install once: `uv tool install madmom-onnx`.");

        var psi = new ProcessStartInfo
        {
            FileName = BinaryPath,
            ArgumentList = { "--beats_per_bar", "3,4", "single", filePath },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var proc = Process.Start(psi)!;
        var stdout = await proc.StandardOutput.ReadToEndAsync(ct);
        var stderr = await proc.StandardError.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);

        if (proc.ExitCode != 0)
            throw new InvalidOperationException($"madmom failed: {stderr}");

        // DBNDownBeatTracker output: each line is "TIME\tBEAT_NUMBER" where
        // BEAT_NUMBER is 1 for downbeat, 2..N for the rest of the bar.
        var beats = new List<double>();
        var downbeats = new List<double>();
        foreach (var line in stdout.Split('\n'))
        {
            var parts = line.Trim().Split('\t');
            if (parts.Length < 1 || parts[0].Length == 0) continue;
            if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var t)) continue;
            beats.Add(t);
            if (parts.Length >= 2 && parts[1].Trim() == "1") downbeats.Add(t);
        }

        // BPM from median inter-beat interval.
        double bpm = 0;
        if (beats.Count >= 2)
        {
            var gaps = new double[beats.Count - 1];
            for (int i = 1; i < beats.Count; i++) gaps[i - 1] = beats[i] - beats[i - 1];
            Array.Sort(gaps);
            double median = gaps[gaps.Length / 2];
            bpm = Math.Round(60.0 / median * 10) / 10.0;
        }

        return (bpm, beats.ToArray(), downbeats.ToArray());
    }

    private static string? FindBinary(string name)
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", name),
            "/usr/local/bin/" + name,
            "/usr/bin/" + name,
        };
        foreach (var c in candidates)
            if (File.Exists(c)) return c;

        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in path.Split(Path.PathSeparator))
        {
            var full = Path.Combine(dir, name);
            if (File.Exists(full)) return full;
        }
        return null;
    }
}
