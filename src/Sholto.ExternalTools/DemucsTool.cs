using System.Globalization;
using System.Text.RegularExpressions;

using Sholto.Analysis;
using Sholto.Analysis.Processing;
using Sholto.Analysis.Stems;
using Sholto.Analysis.ToolBoundary;

namespace Sholto.ExternalTools;

/// <summary>Descriptor for demucs stem separation. Optional — callers keep using
/// whatever they had (undecoded / mono mix) if this fails. Postcondition: the four
/// named stem files exist AND are non-empty under <c>&lt;workDir&gt;/htdemucs/</c> —
/// exit code 0 alone is exactly the condition that silently produced zero stems on
/// 5 Sep 2026.</summary>
/// <summary>Takes the expected output paths by value (computed once by the caller)
/// rather than a back-reference to the analyser that owns them — Verify only ever
/// needed the paths for this one call, not the whole collaborator.</summary>
internal sealed class DemucsTool(StemPaths expectedPaths) : IToolDefinition<StemPaths>
{
    public string Name => AnalysisSteps.Stems;
    public string BinaryName => ExternalToolNames.Demucs;
    public bool IsRequired => false;

    // tqdm-shaped: " 40%|####      | 10/25 [00:12<00:18]" — anchored so a prose line
    // that merely happens to contain a '%' (e.g. a checkpoint-loading warning) is
    // never mistaken for progress.
    private static readonly Regex ProgressRx = new(@"^\s*(\d{1,3})%\|", RegexOptions.Compiled);

    // --filename "{stem}.{ext}" flattens demucs's default per-track basename nesting
    // (<out>/<model>/<track_name>/vocals.wav) down to <out>/<model>/vocals.wav; demucs
    // still appends its own <model>/ (StemPaths.ModelDir) subfolder unconditionally.
    // sholto-deps.sh's check_demucs mirrors this exact --out/--filename pair (and the
    // resulting <out>/htdemucs/<stem>.wav layout) when it verifies a demucs install —
    // the two must not drift.
    public IReadOnlyList<string> BuildArgs(ToolInput toolInput) =>
        new[] { "--out", toolInput.WorkDir, "--filename", "{stem}.{ext}", toolInput.Input };

    public bool TryParseProgress(string line, out double progress)
    {
        var m = ProgressRx.Match(line);
        if (m.Success &&
            int.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pct))
        {
            progress = Math.Clamp(pct / 100.0, 0.0, 1.0);
            return true;
        }
        progress = 0;
        return false;
    }

    public ToolOutcome<StemPaths> Verify(ToolInput toolInput, string stdout, int exitCode)
    {
        if (exitCode != 0)
            return ToolOutcome<StemPaths>.Failure($"demucs exited with code {exitCode}");

        var paths = expectedPaths;
        if (!paths.All.All(p => File.Exists(p) && new FileInfo(p).Length > 0))
            return ToolOutcome<StemPaths>.Failure(
                "demucs exited 0 but expected stem files are missing or empty");

        return ToolOutcome<StemPaths>.Success(paths);
    }
}
