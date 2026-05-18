using System.Diagnostics;
using System.Globalization;

namespace OpenDJ.Audio.Analysis;

/// <summary>
/// Best-in-class beat / downbeat detection: shells out to madmom's
/// DBNDownBeatTracker (RNN + dynamic Bayesian network). Works when madmom
/// is installed somewhere on PATH or under <c>~/.local/bin</c> and ffmpeg
/// is available for decoding.
///
/// Install once:
///   uv tool install madmom-onnx
///   sudo apt install ffmpeg
/// </summary>
public sealed class MadmomBeatAnalyzer : IBeatAnalyzer
{
    public string? BinaryPath { get; }

    public MadmomBeatAnalyzer()
    {
        BinaryPath = FindBinary("DBNDownBeatTracker");
    }

    public bool IsAvailable => BinaryPath is not null;

    public async Task<BeatResult> AnalyzeAsync(string filePath, float[] _, int __, CancellationToken ct)
    {
        if (BinaryPath is null)
            throw new InvalidOperationException("madmom DBNDownBeatTracker not found on this system.");

        // madmom reads the file directly via ffmpeg, much faster than re-encoding
        // our decoded float[] back to wav.
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

        return new BeatResult(bpm, beats.ToArray(), downbeats.ToArray());
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

        // Last resort: PATH search.
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in path.Split(Path.PathSeparator))
        {
            var full = Path.Combine(dir, name);
            if (File.Exists(full)) return full;
        }
        return null;
    }
}
