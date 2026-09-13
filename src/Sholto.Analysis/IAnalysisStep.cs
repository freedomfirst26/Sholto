namespace Sholto.Analysis;

/// <summary>
/// The surface every analyser backed by an arm's-length external tool shares:
/// its reporter step name, where its binary lives, and whether it's usable at all.
/// Extracted because <see cref="IBeatAnalysisStep"/> and <see cref="IStemAnalysisStep"/> each
/// repeated it — and <c>IStemAnalysisStep</c> had silently dropped
/// <c>BinaryPath</c>/<c>IsAvailable</c> entirely (demucs exposed neither), which this
/// closes.
///
/// Deliberately does NOT include <c>AreCached</c>: madmom has no such concept (beat
/// results cache via <c>AnalysisProvider</c>, not a per-track file check), so a base
/// member here would lie for one of the implementers. Nor does it include
/// <c>AnalyzeAsync</c> — each adapter owns its own invocation and result parsing;
/// that is the entire point of having three separate interfaces rather than one.
/// </summary>
public interface IAnalysisStep
{
    /// <summary>Step name used by <see cref="AnalysisReporter"/> (e.g. "beats",
    /// "stems", "segments").</summary>
    string StepName { get; }

    bool IsAvailable { get; }
}
