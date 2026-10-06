namespace Sholto.App.Analysis.Analyzers;

/// <summary>An <see cref="IBasicAnalysisRequest"/> that finishes by running the supplied delegate.</summary>
public sealed class BasicAnalysisRequest(Func<DecodedTrack, Task<BasicAnalysis>> complete) : IBasicAnalysisRequest
{
    private readonly Func<DecodedTrack, Task<BasicAnalysis>> _complete = complete;

    public Task<BasicAnalysis> CompleteAsync(DecodedTrack track) => _complete(track);
}
