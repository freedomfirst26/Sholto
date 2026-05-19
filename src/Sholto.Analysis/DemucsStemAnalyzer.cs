using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Sholto.Analysis;

/// <summary>Paths to the four stem WAV files for one analysed track.</summary>
public sealed record StemPaths(string Vocals, string Drums, string Bass, string Other) : IAnalysis
{
    public string Name => "Stems";
    public IEnumerable<string> All { get { yield return Vocals; yield return Drums; yield return Bass; yield return Other; } }
}

/// <summary>
/// Runs <c>demucs</c> on a track to split it into 4 stems (vocals / drums / bass / other)
/// and caches the resulting WAVs under <c>~/.local/share/sholto/stems/&lt;hash&gt;/</c>.
///
/// Progress is reported through an <see cref="AnalysisReporter"/> using the step name
/// <see cref="StepName"/> — UI code can show a per-track progress chip by listening to
/// the reporter.
///
/// First analysis of a track is slow (~30–180 s on CPU); subsequent runs hit the cache
/// instantly. The demucs binary must be on <c>PATH</c> (install.sh installs it via uv).
/// </summary>
public static class DemucsStemAnalyzer
{
    public const string StepName = "stems";

    private static readonly string CacheRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "sholto", "stems");

    // demucs prints lines like "  20%|██        | 5/25 [00:12<00:48,  ...]"
    // We only need the first percentage we see per buffer to drive progress.
    private static readonly Regex ProgressRx = new(@"(\d{1,3})\s*%", RegexOptions.Compiled);

    /// <summary>
    /// Produce stems for <paramref name="filePath"/>. Returns immediately from cache
    /// if a previous run already wrote the four WAVs; otherwise runs demucs.
    /// </summary>
    public static async Task<StemPaths> AnalyzeAsync(
        string filePath,
        AnalysisReporter? reporter = null,
        CancellationToken ct = default)
    {
        var key = CacheKey(filePath);
        var dir = Path.Combine(CacheRoot, key);
        Directory.CreateDirectory(dir);

        // demucs always nests output inside <out>/<model>/ regardless of --filename.
        // htdemucs is the default model.
        var stemDir = Path.Combine(dir, "htdemucs");

        var paths = new StemPaths(
            Path.Combine(stemDir, "vocals.wav"),
            Path.Combine(stemDir, "drums.wav"),
            Path.Combine(stemDir, "bass.wav"),
            Path.Combine(stemDir, "other.wav"));

        if (paths.All.All(File.Exists))
        {
            reporter?.Complete(filePath, StepName, "cached");
            return paths;
        }

        reporter?.Running(filePath, StepName, 0, "demucs starting");

        // Flatten output so it lands directly in <dir>/<stem>.wav (no nested
        // <model>/<basename>/ folders that demucs creates by default).
        var psi = new ProcessStartInfo("demucs")
        {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
        };
        psi.ArgumentList.Add("--out");      psi.ArgumentList.Add(dir);
        psi.ArgumentList.Add("--filename"); psi.ArgumentList.Add("{stem}.{ext}");
        psi.ArgumentList.Add(filePath);

        using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };

        proc.OutputDataReceived += (_, e) => HandleProgress(e.Data, filePath, reporter);
        proc.ErrorDataReceived  += (_, e) => HandleProgress(e.Data, filePath, reporter);

        if (!proc.Start())
        {
            reporter?.Failed(filePath, StepName, "could not start demucs");
            throw new InvalidOperationException("could not start demucs");
        }
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        await proc.WaitForExitAsync(ct);

        if (proc.ExitCode != 0)
        {
            var msg = $"demucs exited with code {proc.ExitCode}";
            reporter?.Failed(filePath, StepName, msg);
            throw new InvalidOperationException(msg);
        }

        if (!paths.All.All(File.Exists))
        {
            var msg = "demucs finished but expected stem files are missing";
            reporter?.Failed(filePath, StepName, msg);
            throw new InvalidOperationException(msg);
        }

        reporter?.Complete(filePath, StepName, "ok");
        return paths;
    }

    private static void HandleProgress(string? line, string filePath, AnalysisReporter? reporter)
    {
        if (string.IsNullOrEmpty(line) || reporter is null) return;
        var m = ProgressRx.Match(line);
        if (!m.Success) return;
        if (!int.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pct)) return;
        reporter.Running(filePath, StepName, pct / 100.0, $"{pct}%");
    }

    /// <summary>Cache key: SHA-1 of (size + first 1 MiB of bytes). Fast and stable
    /// across file moves; cheap enough to compute on every load.</summary>
    private static string CacheKey(string filePath)
    {
        var fi = new FileInfo(filePath);
        using var sha = SHA1.Create();
        using var fs = File.OpenRead(filePath);
        Span<byte> sizeBytes = stackalloc byte[8];
        BitConverter.TryWriteBytes(sizeBytes, fi.Length);
        sha.TransformBlock(sizeBytes.ToArray(), 0, 8, null, 0);

        var buf = new byte[1 << 20];
        int read = fs.Read(buf, 0, buf.Length);
        sha.TransformFinalBlock(buf, 0, read);
        return Convert.ToHexString(sha.Hash!).ToLowerInvariant();
    }
}
