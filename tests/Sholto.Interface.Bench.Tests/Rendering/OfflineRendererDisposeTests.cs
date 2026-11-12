namespace Sholto.Interface.Bench.Tests.Rendering;

/// <summary><c>OfflineRenderer.RenderScenario</c> unloads the decks it builds, so the source WAV is closed
/// when it returns. Probed with <see cref="FileShare.None"/>, which .NET on Linux backs with an exclusive
/// flock, so a still-open deck stream makes the probe throw there as well as on Windows.</summary>
public sealed class OfflineRendererDisposeTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("bench-dispose").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void RenderScenario_closes_the_source_wav_before_returning()
    {
        string src = Path.Combine(_dir, "src.wav");
        new SyntheticSource(48000, 2.0).WriteWav(src);

        new BenchRenderRig().Render(src, 1.0, Path.Combine(_dir, "out.wav"));

        using var probe = File.Open(src, FileMode.Open, FileAccess.Read, FileShare.None);
    }
}
