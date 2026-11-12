using System.Collections.Concurrent;
using Sholto.App.Analysis;
using Sholto.App.Analysis.Analyzers;

namespace Sholto.App.Audio.Tests.BatchAnalysis;

/// <summary>An <see cref="IAnalysisProvider"/> whose <c>CompleteAsync</c> holds every track at a gate
/// (released by <see cref="Release"/>, or cancelled by the token given to <c>Begin</c>), counts how
/// many are held at once, and throws for <see cref="FailingPath"/>.</summary>
internal sealed class GatedAnalysisProvider : IAnalysisProvider
{
    private readonly SemaphoreSlim _gate = new(0);
    private readonly object _lock = new();
    private int _inFlight;

    public string? FailingPath { get; init; }
    public int MaxInFlight { get; private set; }
    public int InFlight { get { lock (_lock) return _inFlight; } }
    public ConcurrentDictionary<string, CancellationToken> Tokens { get; } = new();

    public void Release(int count) => _gate.Release(count);

    public IBasicAnalysisRequest Begin(string filePath, CancellationToken ct = default)
    {
        Tokens[filePath] = ct;
        return new BasicAnalysisRequest(track => CompleteAsync(track, ct));
    }

    private async Task<BasicAnalysis> CompleteAsync(DecodedTrack track, CancellationToken ct)
    {
        lock (_lock) MaxInFlight = Math.Max(MaxInFlight, ++_inFlight);
        try
        {
            await _gate.WaitAsync(ct);
            if (track.FilePath == FailingPath) throw new InvalidOperationException($"boom: {track.FilePath}");
            return FakeBasicAnalysisStore.Sample();
        }
        finally
        {
            lock (_lock) _inFlight--;
        }
    }

    public Task<BasicAnalysis> GetAsync(DecodedTrack track, CancellationToken ct = default) =>
        Begin(track.FilePath, ct).CompleteAsync(track);

    public Task<BasicAnalysis> RecomputeAsync(DecodedTrack track, CancellationToken ct = default) =>
        GetAsync(track, ct);
}
