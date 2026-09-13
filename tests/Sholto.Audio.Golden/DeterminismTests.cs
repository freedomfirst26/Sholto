using System.Diagnostics;
using Sholto.Bench.Rendering;

namespace Sholto.Audio.Golden;

/// <summary>
/// Establishes the precondition the whole golden-audio design rests on:
/// rendering the same scripted timeline twice, with nothing else different,
/// produces byte-identical output. If any of this were false — a race in the
/// stem swap, uninitialised buffer reuse, thread-timing-dependent output —
/// every earlier "the sound didn't change" proof in this codebase (all of
/// them done by hand, by hashing a throwaway render) would have been luck,
/// and an exact-equality golden test would be unusable (it would fail on its
/// own non-determinism, not on real regressions). So this runs FIRST,
/// checked independently of the golden comparisons in
/// <see cref="GoldenAudioTests"/>, not assumed by them.
/// </summary>
public sealed class DeterminismTests
{
    public static IEnumerable<object[]> AllScenarios()
    {
        yield return new object[] { "gain_crossfade", (Action<string>)Scenarios.RenderGainAndCrossfade };
        yield return new object[] { "eq", (Action<string>)Scenarios.RenderEqBands };
        yield return new object[] { "filter", (Action<string>)Scenarios.RenderFilterSweep };
        yield return new object[] { "echo", (Action<string>)Scenarios.RenderEchoOnOff };
        yield return new object[] { "scratch", (Action<string>)Scenarios.RenderScratch };
    }

    [Theory]
    [MemberData(nameof(AllScenarios))]
    public void SameProcess_TwoRendersOfSameTimeline_AreByteIdentical(string name, Action<string> render)
    {
        string a = Path.Combine(Path.GetTempPath(), $"sholto-det-{name}-a-{Guid.NewGuid():N}.wav");
        string b = Path.Combine(Path.GetTempPath(), $"sholto-det-{name}-b-{Guid.NewGuid():N}.wav");
        try
        {
            render(a);
            render(b);
            byte[] bytesA = File.ReadAllBytes(a);
            byte[] bytesB = File.ReadAllBytes(b);
            Assert.Equal(bytesA.Length, bytesB.Length);
            Assert.True(bytesA.AsSpan().SequenceEqual(bytesB),
                $"{name}: two in-process renders of the identical timeline produced different bytes — " +
                "this scenario is non-deterministic and cannot be used as a golden reference until fixed.");
        }
        finally { File.Delete(a); File.Delete(b); }
    }

    /// <summary>Same proof, across an OS process boundary, via Sholto.Bench's
    /// own `render` CLI subcommand (the one a human/agent actually runs) so
    /// this also covers the file-decode (LoadStreaming) path that the
    /// in-process <see cref="Scenarios"/> deliberately bypass (they use
    /// <c>Deck.Load</c> with in-memory samples — see that class's doc).
    /// Builds a tiny synthetic fixture at test time (never checked in — see
    /// <see cref="Signal"/>) purely to have a --track argument to pass.</summary>
    [Fact]
    public void CrossProcess_TwoRendersOfSameTrack_AreByteIdentical()
    {
        string repoRoot = FindRepoRoot();
        string benchProj = Path.Combine(repoRoot, "tools", "Sholto.Bench", "Sholto.Bench.csproj");
        string fixturePath = Path.Combine(Path.GetTempPath(), $"sholto-det-fixture-{Guid.NewGuid():N}.wav");
        string outA = Path.Combine(Path.GetTempPath(), $"sholto-det-cross-a-{Guid.NewGuid():N}.wav");
        string outB = Path.Combine(Path.GetTempPath(), $"sholto-det-cross-b-{Guid.NewGuid():N}.wav");
        try
        {
            using (var writer = new WavWriter(fixturePath, Signal.SampleRate, channels: 2))
                writer.WriteInterleaved(Signal.ToneStackA(0.6));

            RunBenchRender(benchProj, fixturePath, outA);
            RunBenchRender(benchProj, fixturePath, outB);

            byte[] bytesA = File.ReadAllBytes(outA);
            byte[] bytesB = File.ReadAllBytes(outB);
            Assert.True(bytesA.Length > 44, "rendered WAV is suspiciously small — did the CLI actually run?");
            Assert.Equal(bytesA.Length, bytesB.Length);
            Assert.True(bytesA.AsSpan().SequenceEqual(bytesB),
                "two separate-process runs of `Sholto.Bench render` against the same track produced " +
                "different output bytes — cross-process non-determinism in the real render path.");
        }
        finally
        {
            File.Delete(fixturePath);
            File.Delete(outA);
            File.Delete(outB);
        }
    }

    /// <summary>Invokes the ALREADY-BUILT Sholto.Bench.dll via `dotnet exec`,
    /// never `dotnet run` — `dotnet run` re-evaluates the whole project graph
    /// (MSBuild + restore checks) on every call, which is both slow (each
    /// invocation spins up a fresh MSBuild node pool) and, worse for a
    /// determinism proof, adds a pile of build-tool activity between the two
    /// runs that has nothing to do with the code under test. `dotnet build`
    /// runs once (memoised for the test process's lifetime); each render is
    /// then just `dotnet exec <dll> render ...` — the same binary, twice.</summary>
    private static readonly System.Threading.SemaphoreSlim s_buildLock = new(1, 1);
    private static string? s_benchDllPath;

    private static string EnsureBenchBuilt(string benchProj)
    {
        s_buildLock.Wait();
        try
        {
            if (s_benchDllPath is not null) return s_benchDllPath;

            var build = new ProcessStartInfo("dotnet")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (var arg in new[] { "build", benchProj, "-nologo", "-v", "quiet" })
                build.ArgumentList.Add(arg);
            using (var buildProcess = Process.Start(build) ?? throw new InvalidOperationException("failed to start dotnet build"))
            {
                string bOut = buildProcess.StandardOutput.ReadToEnd();
                string bErr = buildProcess.StandardError.ReadToEnd();
                buildProcess.WaitForExit();
                if (buildProcess.ExitCode != 0)
                    throw new InvalidOperationException($"`dotnet build {benchProj}` exited {buildProcess.ExitCode}.\n{bOut}\n{bErr}");
            }

            string binDir = Path.Combine(Path.GetDirectoryName(benchProj)!, "bin");
            string? dll = Directory.EnumerateFiles(binDir, "Sholto.Bench.dll", SearchOption.AllDirectories)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
            if (dll is null)
                throw new FileNotFoundException($"Sholto.Bench.dll not found under {binDir} after a successful build");
            s_benchDllPath = dll;
            return dll;
        }
        finally { s_buildLock.Release(); }
    }

    private static void RunBenchRender(string benchProj, string track, string outPath)
    {
        string dll = EnsureBenchBuilt(benchProj);
        var psi = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var arg in new[]
        {
            "exec", dll,
            "render", "--track", track, "--duration", "0.4", "--out", outPath,
        }) psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("failed to start dotnet");
        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"`dotnet exec {dll} render ...` exited {process.ExitCode}.\nstdout:\n{stdout}\nstderr:\n{stderr}");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Sholto.slnx"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException($"could not find Sholto.slnx above {AppContext.BaseDirectory}");
    }
}
