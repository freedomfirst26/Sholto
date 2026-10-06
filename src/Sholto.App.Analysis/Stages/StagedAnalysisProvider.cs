using Sholto.App.Analysis.Analyzers;

namespace Sholto.App.Analysis.Stages;

/// <summary>Compute tier of the basic-analysis provider chain: every call runs the basic stage.</summary>
public sealed class StagedAnalysisProvider(IBasicAnalysisStage stage) : IAnalysisProvider
{
    private readonly IBasicAnalysisStage _stage = stage;

    public Task<BasicAnalysis> GetAsync(DecodedTrack track, CancellationToken ct = default) =>
        _stage.RunAsync(track, ct);

    public Task<BasicAnalysis> RecomputeAsync(DecodedTrack track, CancellationToken ct = default) =>
        _stage.RunAsync(track, ct);

    public IBasicAnalysisRequest Begin(string filePath, CancellationToken ct = default) =>
        _stage.Begin(filePath, ct);
}
