using System.Diagnostics;
using Sholto.Analysis;
using Sholto.ExternalTools;

namespace Sholto.App.Tests;

public class ExternalToolOptionsTests
{
    [Fact]
    public void ForCurrentPlatform_returns_Linux_options_on_Linux()
    {
        // This suite only needs to run on Linux (per CLAUDE.md); assert Linux()
        // directly and separately assert ForCurrentPlatform delegates correctly
        // without requiring the test itself to run on Windows.
        var linux = ExternalToolOptionsFactory.Linux();
        Assert.Equal("", linux.ExecutableSuffix);
        Assert.Contains(linux.SearchDirectories, d => d.EndsWith("/.local/bin"));
        Assert.Contains("/usr/local/bin", linux.SearchDirectories);
        Assert.Contains("/usr/bin", linux.SearchDirectories);

        if (!OperatingSystem.IsWindows())
        {
            var current = ExternalToolOptionsFactory.ForCurrentPlatform();
            Assert.Equal(linux.ExecutableSuffix, current.ExecutableSuffix);
            Assert.Equal(linux.SearchDirectories, current.SearchDirectories);
        }
    }

    [Fact]
    public void Windows_options_append_dot_exe_and_search_windows_dirs()
    {
        // A Windows-shaped options object, built and exercised without needing to
        // actually run on Windows.
        var windows = ExternalToolOptionsFactory.Windows();
        Assert.Equal(".exe", windows.ExecutableSuffix);
        Assert.NotEmpty(windows.SearchDirectories);
        Assert.All(windows.SearchDirectories, d => Assert.DoesNotContain("/usr/", d));

        var finder = new ExternalToolFinder(
            windows, windows.SearchDirectories, pathEnv: null);
        var dir = windows.SearchDirectories[0];
        Directory.CreateDirectory(dir);
        var fake = Path.Combine(dir, "demucs.exe");
        try
        {
            File.WriteAllText(fake, "fake");
            var found = finder.Locate("demucs");
            Assert.Equal(fake, found);
        }
        finally
        {
            File.Delete(fake);
        }
    }

    [Fact]
    public void Defaults_carry_every_tool_name()
    {
        // The old MadmomName/DemucsName/FfmpegName scalars (and
        // DemucsModelDir) are gone from ExternalToolOptions — the tool list is
        // now a single collection, and the demucs model dir moved to
        // DemucsTool.ModelDir (an adapter-owned output-layout fact, not a
        // machine fact this type should carry).
        var options = ExternalToolOptionsFactory.Linux();
        Assert.Equal(ExternalToolNames.All, options.Tools);
    }
}

public class ExternalToolFinderTests
{
    private static string WriteFake(string dir, string name)
    {
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, name);
        File.WriteAllText(path, "#!/bin/sh\necho fake\n");
        return path;
    }

    private static ExternalToolFinder MakeFinder(IEnumerable<string> fixedDirs, string? pathEnv) =>
        new(ExternalToolOptionsFactory.Linux(), fixedDirs, pathEnv);

    [Fact]
    public void First_fixed_dir_wins_over_later_fixed_dirs_and_PATH()
    {
        var root = Path.Combine(Path.GetTempPath(), "sholto_resolve_" + Guid.NewGuid().ToString("N"));
        var localBin = Path.Combine(root, "local_bin");
        var usrLocalBin = Path.Combine(root, "usr_local_bin");
        var usrBin = Path.Combine(root, "usr_bin");
        var pathDir = Path.Combine(root, "path_dir");
        try
        {
            var expected = WriteFake(localBin, "sometool");
            WriteFake(usrLocalBin, "sometool");
            WriteFake(usrBin, "sometool");
            WriteFake(pathDir, "sometool");

            var finder = MakeFinder(new[] { localBin, usrLocalBin, usrBin }, pathDir);
            var found = finder.Locate("sometool");

            Assert.Equal(expected, found);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Second_fixed_dir_wins_when_first_lacks_the_binary()
    {
        var root = Path.Combine(Path.GetTempPath(), "sholto_resolve_" + Guid.NewGuid().ToString("N"));
        var localBin = Path.Combine(root, "local_bin");
        var usrLocalBin = Path.Combine(root, "usr_local_bin");
        var usrBin = Path.Combine(root, "usr_bin");
        try
        {
            Directory.CreateDirectory(localBin); // exists but has no binary
            var expected = WriteFake(usrLocalBin, "sometool");
            WriteFake(usrBin, "sometool");

            var finder = MakeFinder(new[] { localBin, usrLocalBin, usrBin }, pathEnv: null);
            var found = finder.Locate("sometool");

            Assert.Equal(expected, found);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Falls_back_to_PATH_when_no_fixed_dir_has_it()
    {
        var root = Path.Combine(Path.GetTempPath(), "sholto_resolve_" + Guid.NewGuid().ToString("N"));
        var localBin = Path.Combine(root, "local_bin");
        var usrLocalBin = Path.Combine(root, "usr_local_bin");
        var usrBin = Path.Combine(root, "usr_bin");
        var pathDir1 = Path.Combine(root, "path_dir_1");
        var pathDir2 = Path.Combine(root, "path_dir_2");
        try
        {
            Directory.CreateDirectory(localBin);
            Directory.CreateDirectory(usrLocalBin);
            Directory.CreateDirectory(usrBin);
            Directory.CreateDirectory(pathDir1); // first PATH entry, no binary
            var expected = WriteFake(pathDir2, "sometool");

            var pathEnv = string.Join(Path.PathSeparator, pathDir1, pathDir2);
            var finder = MakeFinder(new[] { localBin, usrLocalBin, usrBin }, pathEnv);
            var found = finder.Locate("sometool");

            Assert.Equal(expected, found);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Returns_null_when_not_found_anywhere()
    {
        var finder = MakeFinder(Array.Empty<string>(), pathEnv: "");
        var found = finder.Locate("sholto_definitely_does_not_exist_xyz");
        Assert.Null(found);
    }

    [Fact]
    public void LocateOrName_falls_back_to_the_suffixed_bare_name()
    {
        var finder = MakeFinder(Array.Empty<string>(), pathEnv: "");
        Assert.Equal("sholto_definitely_does_not_exist_xyz", finder.LocateOrName("sholto_definitely_does_not_exist_xyz"));
    }
}

public class ExternalToolRunnerTests
{
    private static ExternalToolRunner MakeRunner() => new();

    private static readonly ExternalToolFinder Finder = new(ExternalToolOptionsFactory.ForCurrentPlatform());

    /// <summary>A minimal descriptor that runs a real /bin/sh process so the runner's
    /// process-pumping machinery is exercised end to end.</summary>
    private sealed class ShTool : IToolDefinition<string>
    {
        public required string Script { get; init; }
        public Func<string, string, string, int, ToolOutcome<string>>? VerifyOverride { get; init; }

        public string Name => "sh-tool";
        public string BinaryName => "sh";
        public bool IsRequired => false;

        public IReadOnlyList<string> BuildArgs(string input, string workDir) => new[] { "-c", Script };

        public ToolOutcome<string> Verify(string input, string workDir, string stdout, int exitCode) =>
            VerifyOverride is not null
                ? VerifyOverride(input, workDir, stdout, exitCode)
                : (exitCode == 0
                    ? ToolOutcome<string>.Success(stdout)
                    : ToolOutcome<string>.Failure($"exited {exitCode}"));
    }

    private static readonly string? ShPath = Finder.Locate("sh");
    private static readonly bool ShAvailable = ShPath is not null;

    [Fact]
    public async Task Exit_zero_with_failing_postcondition_is_reported_Failed_not_Complete()
    {
        if (!ShAvailable) return; // sh should always be present on Linux CI, but guard anyway

        var reporter = new AnalysisReporter(Array.Empty<string>());
        const string path = "/music/track.mp3";

        // Exits 0 (echo always succeeds) but Verify always says the postcondition
        // failed — exactly the shape of the 5 Sep 2026 demucs incident.
        var tool = new ShTool
        {
            Script = "echo hello",
            VerifyOverride = (_, _, _, exitCode) => ToolOutcome<string>.Failure("postcondition not met"),
        };

        var outcome = await MakeRunner().RunAsync(tool, ShPath, path, "/tmp", reporter);

        Assert.False(outcome.IsSuccess);
        var step = reporter.ReportFor(path).Steps[tool.Name];
        Assert.Equal(AnalysisState.Failed, step.State);
        Assert.NotEqual(AnalysisState.Complete, step.State);
        Assert.Contains("postcondition not met", step.Message);
    }

    [Fact]
    public async Task A_diagnostic_line_containing_percent_still_reaches_the_tail()
    {
        if (!ShAvailable) return;

        var reporter = new AnalysisReporter(Array.Empty<string>());
        const string path = "/music/track.mp3";

        // A line that contains a '%' but is NOT a progress line (no tqdm-style
        // "NN%|" prefix) must still end up in the failure tail, not be swallowed.
        var tool = new ShTool
        {
            Script = "echo 'Only 50% of the checkpoint weights were loaded'; exit 1",
            VerifyOverride = (_, _, _, exitCode) => ToolOutcome<string>.Failure($"exited {exitCode}"),
        };

        var outcome = await MakeRunner().RunAsync(tool, ShPath, path, "/tmp", reporter);

        Assert.False(outcome.IsSuccess);
        Assert.Contains("Only 50% of the checkpoint weights were loaded", outcome.Reason);
    }

    [Fact]
    public async Task Success_reaches_Complete_with_the_verified_value()
    {
        if (!ShAvailable) return;

        var reporter = new AnalysisReporter(Array.Empty<string>());
        const string path = "/music/track.mp3";
        var tool = new ShTool { Script = "echo ok" };

        var outcome = await MakeRunner().RunAsync(tool, ShPath, path, "/tmp", reporter);

        Assert.True(outcome.IsSuccess);
        var step = reporter.ReportFor(path).Steps[tool.Name];
        Assert.Equal(AnalysisState.Complete, step.State);
    }

    [Fact]
    public async Task Progress_is_clamped_to_0_and_1()
    {
        if (!ShAvailable) return;

        var reporter = new AnalysisReporter(Array.Empty<string>());
        const string path = "/music/track.mp3";

        // A tool whose progress parser reports an out-of-range fraction (e.g. from a
        // 3-digit percentage like "999%") must still end up clamped in the reporter.
        var tool = new ClampingTool();

        await MakeRunner().RunAsync(tool, ShPath, path, "/tmp", reporter);

        var step = reporter.ReportFor(path).Steps[tool.Name];
        Assert.InRange(step.Progress, 0.0, 1.0);
    }

    private sealed class ClampingTool : IToolDefinition<string>
    {
        public string Name => "clamp-tool";
        public string BinaryName => "sh";
        public bool IsRequired => false;

        public IReadOnlyList<string> BuildArgs(string input, string workDir) =>
            new[] { "-c", "echo '999%|xxx'" };

        public ToolOutcome<string> Verify(string input, string workDir, string stdout, int exitCode) =>
            ToolOutcome<string>.Success(stdout);

        public bool TryParseProgress(string line, out double progress)
        {
            if (line.Contains('%'))
            {
                // Deliberately out of range before the runner clamps it.
                progress = 9.99;
                return true;
            }
            progress = 0;
            return false;
        }
    }

    [Fact]
    public async Task Missing_binary_is_reported_Failed_without_starting_a_process()
    {
        var reporter = new AnalysisReporter(Array.Empty<string>());
        const string path = "/music/track.mp3";
        var tool = new MissingBinaryTool();

        var outcome = await MakeRunner().RunAsync(tool, binaryPath: null, path, "/tmp", reporter);

        Assert.False(outcome.IsSuccess);
        Assert.Contains("not found", outcome.Reason);
        var step = reporter.ReportFor(path).Steps[tool.Name];
        Assert.Equal(AnalysisState.Failed, step.State);
    }

    private sealed class MissingBinaryTool : IToolDefinition<string>
    {
        public string Name => "missing-tool";
        public string BinaryName => "sholto_definitely_does_not_exist_xyz";
        public bool IsRequired => false;
        public IReadOnlyList<string> BuildArgs(string input, string workDir) => Array.Empty<string>();
        public ToolOutcome<string> Verify(string input, string workDir, string stdout, int exitCode) =>
            ToolOutcome<string>.Success("unreachable");
    }
}

/// <summary>
/// End-to-end proof against the real tools on this machine — mirrors the pattern
/// FfmpegDecodeStrategyTests uses (skip rather than fail when a tool isn't
/// installed), so CI machines without demucs/madmom installed don't fail here.
/// </summary>
public class RealToolIntegrationTests
{
    private static readonly ExternalToolFinder Finder =
        new(ExternalToolOptionsFactory.ForCurrentPlatform());
    private static readonly ExternalToolOptions Options = ExternalToolOptionsFactory.ForCurrentPlatform();
    private static readonly ExternalToolRunner Runner = new();

    private static string CreateSineWavFixture(double seconds = 2.0)
    {
        var ffmpeg = Finder.Locate("ffmpeg") ?? "ffmpeg";
        var path = Path.Combine(Path.GetTempPath(), $"sholto_test_sine_{Guid.NewGuid():N}.wav");
        var psi = new ProcessStartInfo
        {
            FileName = ffmpeg,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add("-y");
        psi.ArgumentList.Add("-f"); psi.ArgumentList.Add("lavfi");
        psi.ArgumentList.Add("-i"); psi.ArgumentList.Add("sine=frequency=440:sample_rate=44100:duration=" +
            seconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
        psi.ArgumentList.Add(path);
        using var proc = Process.Start(psi)!;
        proc.WaitForExit();
        if (proc.ExitCode != 0)
            throw new InvalidOperationException("failed to generate wav fixture: " + proc.StandardError.ReadToEnd());
        return path;
    }

    [Fact]
    public async Task Madmom_real_binary_produces_beats_for_a_real_file()
    {
        var madmomSession = new ConfiguredTool(
            Runner, Finder.Locate(ExternalToolNames.Madmom),
            path => Path.GetDirectoryName(path) ?? "");
        var madmom = new MadmomBeatAnalyzer(madmomSession);
        if (!madmom.IsAvailable) return;

        var fixture = CreateSineWavFixture(6.0);
        try
        {
            var (bpm, beats, _) = await madmom.AnalyzeAsync(fixture);
            // A pure sine tone has no real beat structure, so we only assert the
            // postcondition the new Verify enforces: at least one beat was parsed
            // from real stdout of a real process — proof the runner→Verify wiring
            // for a required tool works end to end, not just against a mock.
            Assert.True(beats.Length >= 1);
        }
        finally
        {
            File.Delete(fixture);
        }
    }

    [Fact]
    public async Task Demucs_real_binary_produces_four_nonempty_stems()
    {
        var demucsPath = Finder.Locate(ExternalToolNames.Demucs);
        if (demucsPath is null) return;

        var cacheRoot = Path.Combine(Path.GetTempPath(), $"sholto_test_stems_{Guid.NewGuid():N}");
        var demucsSession = new ConfiguredTool(Runner, demucsPath, _ => cacheRoot);
        var demucs = new DemucsStemAnalyzer(demucsSession);
        var fixture = CreateSineWavFixture(3.0);
        try
        {
            var stems = await demucs.AnalyzeAsync(fixture);
            foreach (var s in stems.All)
            {
                Assert.True(File.Exists(s), $"missing stem file {s}");
                Assert.True(new FileInfo(s).Length > 0, $"empty stem file {s}");
            }
        }
        finally
        {
            File.Delete(fixture);
        }
    }
}
