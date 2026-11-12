using System.Collections.Concurrent;
using Sholto.App.Analysis;
using Sholto.App.Analysis.Analyzers.Keys;
using Sholto.App.Analysis.Reporting;

namespace Sholto.App.Audio.Tests.BatchAnalysis;

/// <summary>An <see cref="IKeyAnalyzer"/> that records the token it was given and, when
/// <see cref="Hold"/> is set, waits on it until cancelled.</summary>
internal sealed class GatedKeyAnalyzer : IKeyAnalyzer
{
    public bool Hold { get; init; }
    public ConcurrentDictionary<string, CancellationToken> Tokens { get; } = new();

    public async Task<KeyAnalysis> AnalyzeAsync(DecodedTrack track, IAnalysisReporter reporter, CancellationToken ct = default)
    {
        Tokens[track.FilePath] = ct;
        if (Hold) await Task.Delay(Timeout.Infinite, ct);
        return new KeyAnalysis(null);
    }
}
