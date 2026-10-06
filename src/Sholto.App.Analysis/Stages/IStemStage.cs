using Sholto.App.Analysis.Stems;

namespace Sholto.App.Analysis.Stages;

/// <summary>The stem stage, plus the one fact the orchestrator needs to schedule it.</summary>
public interface IStemStage : IAnalysisStage<StemAnalysis>
{
    /// <summary>Whether separation may run alongside basic analysis. True only once demucs is
    /// confirmed to run on CUDA; on CPU it would starve the beatgrid analysis, so stems wait.</summary>
    Task<bool> CanOverlapBasicAnalysisAsync(CancellationToken ct = default);

    /// <summary>Run the stem stage from the file path and the decoder's output format alone, so it can
    /// start before the track is decoded.</summary>
    Task<StemAnalysis> RunAsync(string filePath, int sampleRate, int channels, CancellationToken ct = default);
}
