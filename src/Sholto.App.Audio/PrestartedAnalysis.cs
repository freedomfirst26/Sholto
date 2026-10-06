using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis.Stems;

namespace Sholto.App.Audio;

/// <summary>Path-only analysis started at <c>BeginLoad</c>, while the file is still decoding, and
/// adopted by the <c>Load</c> of the same path. <see cref="Basic"/> is the started basic request;
/// <see cref="Stems"/> yields the stem run when stems may overlap basic analysis, else null.
/// Nothing here is applied to the deck until it is adopted. App thread.</summary>
internal sealed record PrestartedAnalysis(
    int Generation,
    string FilePath,
    Task<IBasicAnalysisRequest> Basic,
    Task<Task<StemAnalysis>?> Stems);
