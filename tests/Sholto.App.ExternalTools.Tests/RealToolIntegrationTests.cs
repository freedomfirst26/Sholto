using Sholto.App.Analysis.Stems;
using System.Diagnostics;
using Sholto.App.Analysis.Reporting;

namespace Sholto.App.ExternalTools.Tests;

/// <summary>
/// End-to-end proof against the real tools on this machine — mirrors the pattern
/// FfmpegDecodeStrategyTests uses (skip rather than fail when a tool isn't
/// installed), so CI machines without demucs/madmom installed don't fail here.
/// </summary>
public class RealToolIntegrationTests
{
    private readonly ExternalToolFinder _finder =
        new(new ExternalToolOptionsFactory(new ExternalToolCatalog()).ForCurrentPlatform());
    private readonly ExternalToolRunner _runner = new();

    private string CreateSineWavFixture(double seconds = 2.0)
    {
        var ffmpeg = _finder.Locate("ffmpeg") ?? "ffmpeg";
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
        // Built as ExternalToolStackFactory builds it. The step now derives madmom's
        // workspace (the source file's own directory) itself.
        var madmomSession = new ConfiguredTool(_runner, _finder.Locate(ExternalToolNames.Madmom));
        var madmom = new MadmomBeatAnalysisStepFactory().Create(madmomSession, new NullAnalysisReporter());
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
        var demucsPath = _finder.Locate(ExternalToolNames.Demucs);
        if (demucsPath is null) return;

        var cacheRoot = Path.Combine(Path.GetTempPath(), $"sholto_test_stems_{Guid.NewGuid():N}");
        var demucsSession = new ConfiguredTool(_runner, demucsPath);
        var demucs = new DemucsStemAnalysisStep(demucsSession, _ => cacheRoot, new FixedStemDevice(StemDevice.Cpu));
        var fixture = CreateSineWavFixture(3.0);
        try
        {
            var stems = await demucs.AnalyzeAsync(fixture, new NullAnalysisReporter());
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
