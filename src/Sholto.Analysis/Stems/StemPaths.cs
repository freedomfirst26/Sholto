using Sholto.Analysis.Analyzers;

namespace Sholto.Analysis.Stems;

/// <summary>Paths to the four stem WAV files for one analysed track.</summary>
public sealed record StemPaths(string Vocals, string Drums, string Bass, string Other) : IAnalyzer
{
    public string Name => "Stems";
    public IEnumerable<string> All { get { yield return Vocals; yield return Drums; yield return Bass; yield return Other; } }

    /// <summary>The demucs output subfolder demucs itself creates under <c>--out</c>
    /// (named after the separation model, <c>htdemucs</c> by default — pinned, not a
    /// machine fact). This is a real contract: <c>sholto-deps.sh</c> asserts the exact
    /// same <c>htdemucs/&lt;stem&gt;.wav</c> layout, and <see cref="DemucsStemPresence"/>
    /// builds every cached stem path from this value — changing it orphans every
    /// existing user's cache.
    ///
    /// Lives here (not on the <c>Sholto.ExternalTools</c> demucs tool descriptor)
    /// because both <c>Sholto.ExternalTools.DemucsTool</c> (verifying a run's output)
    /// and <see cref="DemucsStemPresence"/>/<c>DemucsStemAnalysisStep</c> in this project
    /// (deciding where a run should write, and whether it already has) need to agree
    /// on the exact same layout — a helper shared by both sides of that boundary has
    /// to live on the side that doesn't depend on the other, which after the
    /// dependency inversion is <c>Sholto.Analysis</c>.</summary>
    public const string ModelDir = "htdemucs";

    /// <summary>The four stems demucs writes, in the order this record declares
    /// them. Named here rather than in the tool descriptor because the filenames,
    /// <see cref="ModelDir"/> and demucs's <c>--filename</c> flag are three halves of
    /// ONE contract with demucs — split across types and they drift, and when they
    /// drift demucs exits 0 and Sholto finds nothing (the 5 Sep 2026 failure).</summary>
    private static readonly string[] StemFiles = ["vocals.wav", "drums.wav", "bass.wav", "other.wav"];

    /// <summary>Where demucs will write its stems given a work directory — the caller
    /// supplies the workspace (filesystem policy); this supplies the layout inside it
    /// (demucs's business). Pure function, no I/O.</summary>
    public static StemPaths PathsIn(string workDir)
    {
        var dir = Path.Combine(workDir, ModelDir);
        return new StemPaths(
            Path.Combine(dir, StemFiles[0]),
            Path.Combine(dir, StemFiles[1]),
            Path.Combine(dir, StemFiles[2]),
            Path.Combine(dir, StemFiles[3]));
    }
}
