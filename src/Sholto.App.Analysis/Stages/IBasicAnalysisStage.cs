using Sholto.App.Analysis.Analyzers;

namespace Sholto.App.Analysis.Stages;

/// <summary>The basic stage, with the path-only part (the beat tracker) startable before the track is decoded.</summary>
public interface IBasicAnalysisStage : IAnalysisStage<BasicAnalysis>
{
    /// <summary>Start the work that needs only the file path and return at once; finish with
    /// <see cref="IBasicAnalysisRequest.CompleteAsync"/> once samples exist.</summary>
    IBasicAnalysisRequest Begin(string filePath, CancellationToken ct = default);
}
