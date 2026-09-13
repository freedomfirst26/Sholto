using Sholto.Analysis.Analyzers;

namespace Sholto.Analysis;

/// <summary>
/// One track's musical key. Lives on <c>TrackAnalysis</c> alongside Basic
/// and StemPaths so each kind of analysis is independent — Basic can land before
/// Key, Key before Stems, etc. CamelotKeys provides the layer that turns these
/// codes into "compatible with my current deck" decisions for row tinting.
/// </summary>
public sealed record KeyAnalysis(string KeyName, string Camelot) : IAnalyzer
{
    public string Name => "Key";
    public static KeyAnalysis Empty { get; } = new("", "");
}
