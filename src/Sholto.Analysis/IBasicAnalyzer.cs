using Sholto.Analysis.Analyzers;

namespace Sholto.Analysis;

/// <summary>
/// Port for <see cref="BasicAnalyzer"/>. Lets a test/Bench harness supply a fake
/// basic-analysis compute step without wiring the real beat/peak/beatgrid/reporter
/// collaborators.
/// </summary>
public interface IBasicAnalyzer
{
    /// <summary>
    /// Build waveform peaks from the decoded samples and run the beat analysis
    /// step for BPM + beats + downbeats. Reports progress through the reporter
    /// this instance was constructed with.
    /// </summary>
    Task<BasicAnalysis> ComputeAsync(
        DecodedTrack track,
        CancellationToken ct = default);
}
