using Sholto.App.Analysis.Stems;

namespace Sholto.TestSupport;

/// <summary>A separator for tests that never separate stems: demucs reads as not installed, so a re-analysis skips stems.</summary>
public sealed class UnavailableStemSeparator : IStemSeparator
{
    public bool IsAvailable => false;

    public Task<StemPaths> SeparateAsync(string filePath, CancellationToken ct = default) =>
        throw new InvalidOperationException("Stems are unavailable in this test.");
}
