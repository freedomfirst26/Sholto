using Sholto.App.Analysis.Harmony;

namespace Sholto.App.Analysis.Analyzers.Keys;

/// <summary>
/// One track's musical key, or null when no key could be determined. Lives on
/// <c>TrackAnalysis</c> alongside Basic and StemPaths so each kind of analysis
/// is independent — Basic can land before Key, Key before Stems, etc.
/// Key.Compatibility turns these keys into "compatible with my current deck"
/// decisions for row tinting. Camelot notation is only a rendering of the key
/// (<see cref="Key.ToCamelot"/>).
/// </summary>
public sealed record KeyAnalysis(Key? Key);
