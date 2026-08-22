using System.Diagnostics;
using System.Text.Json;

namespace Sholto.Analysis;

/// <summary>
/// Real, model-based song-structure analysis via the <c>allin1</c> CLI
/// (mir-aidj/all-in-one — "All-In-One Music Structure Analyzer"). Produces
/// functional sections with labels (intro / verse / chorus / bridge / outro …)
/// aligned to downbeats, which we map onto <see cref="SongSegments"/> to drive the
/// minimap.
///
/// Follows the same shape as <see cref="DemucsStemAnalyzer"/>: shells out to a binary
/// on PATH, caches the JSON under <c>~/.local/share/sholto/segments/&lt;dir&gt;/</c>, and
/// is slow first-run / instant cached. It is OPTIONAL — if <c>allin1</c> isn't
/// installed the deck falls back to the cheap energy heuristic
/// (<see cref="SongSegmentAnalyzer"/>). Install: see install.sh (heavy: PyTorch + NATTEN).
/// </summary>
public static class AllInOneSegmentAnalyzer
{
    public const string StepName = "segments";

    private static readonly string CacheRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "sholto", "segments");

    /// <summary>Path to the allin1 binary, or null if it isn't installed.</summary>
    public static string? BinaryPath { get; } = FindBinary("allin1");
    public static bool IsAvailable => BinaryPath is not null;

    public static bool AreCached(string filePath) => File.Exists(JsonPathFor(filePath));

    private static string JsonPathFor(string filePath)
    {
        var dir = Path.Combine(CacheRoot, DirNameFor(filePath));
        return Path.Combine(dir, Path.GetFileNameWithoutExtension(filePath) + ".json");
    }

    private static string DirNameFor(string filePath)
    {
        var full = Path.GetFullPath(filePath);
        var bad = Path.GetInvalidFileNameChars();
        var sb = new System.Text.StringBuilder(full.Length);
        foreach (var c in full)
            sb.Append(c == '/' || c == '\\' || c == ' ' || bad.Contains(c) ? '_' : c);
        return sb.ToString().TrimStart('_');
    }

    /// <summary>
    /// Analyse structure for <paramref name="filePath"/> with allin1. Returns null if
    /// allin1 isn't installed or the run fails — callers keep the heuristic segments.
    /// Cached after the first run.
    /// </summary>
    public static async Task<SongSegments?> AnalyzeAsync(
        string filePath, AnalysisReporter? reporter = null, CancellationToken ct = default)
    {
        if (BinaryPath is null) return null;

        var jsonPath = JsonPathFor(filePath);
        if (!File.Exists(jsonPath))
        {
            var outDir = Path.GetDirectoryName(jsonPath)!;
            Directory.CreateDirectory(outDir);
            reporter?.Running(filePath, StepName, 0, "allin1 starting");

            var psi = new ProcessStartInfo(BinaryPath)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            psi.ArgumentList.Add("-o"); psi.ArgumentList.Add(outDir);
            psi.ArgumentList.Add(filePath);

            using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            try
            {
                if (!proc.Start()) { reporter?.Failed(filePath, StepName, "could not start allin1"); return null; }
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                await proc.WaitForExitAsync(ct);
                if (proc.ExitCode != 0 || !File.Exists(jsonPath))
                {
                    reporter?.Failed(filePath, StepName, $"allin1 exit {proc.ExitCode}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                reporter?.Failed(filePath, StepName, ex.Message);
                return null;
            }
        }

        try
        {
            var segments = Parse(await File.ReadAllTextAsync(jsonPath, ct));
            reporter?.Complete(filePath, StepName, $"{segments.Segments.Count} sections");
            return segments;
        }
        catch (Exception ex)
        {
            reporter?.Failed(filePath, StepName, "parse: " + ex.Message);
            return null;
        }
    }

    /// <summary>Map an allin1 JSON result to our <see cref="SongSegments"/>.</summary>
    internal static SongSegments Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("segments", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return SongSegments.Empty;

        var list = new List<SongSegment>();
        foreach (var s in arr.EnumerateArray())
        {
            double start = s.TryGetProperty("start", out var st) ? st.GetDouble() : 0;
            double end = s.TryGetProperty("end", out var en) ? en.GetDouble() : start;
            string label = s.TryGetProperty("label", out var lb) ? (lb.GetString() ?? "") : "";
            if (end <= start) continue; // skip the zero-length start/end markers
            list.Add(new SongSegment(start, end, MapLabel(label), 0f));
        }
        return new SongSegments(list);
    }

    // Harmonix label set → our SegmentKind. allin1 has no explicit "drop"; the
    // high-energy section is labelled "chorus".
    private static SegmentKind MapLabel(string label) => label.ToLowerInvariant() switch
    {
        "intro" or "start" => SegmentKind.Intro,
        "outro" or "end"   => SegmentKind.Outro,
        "chorus"           => SegmentKind.Chorus,
        "verse"            => SegmentKind.Verse,
        "bridge"           => SegmentKind.Bridge,
        "break"            => SegmentKind.Breakdown,
        "inst" or "solo"   => SegmentKind.Bridge,
        _                  => SegmentKind.Verse,
    };

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
