using System.Collections.Concurrent;
using Sholto.App.Analysis.Analyzers;
using Sholto.Data;
using Sholto.App.Analysis.Stores;

namespace Sholto.App.Audio.Tests.BatchAnalysis;

/// <summary>An in-memory <see cref="IBasicAnalysisStore"/>; <see cref="Store"/> marks a path as already analysed.</summary>
internal sealed class FakeBasicAnalysisStore : IBasicAnalysisStore
{
    private readonly ConcurrentDictionary<string, BasicAnalysis> _stored = new();

    public static BasicAnalysis Sample() =>
        new(new WaveformPeaks([], [], [], [], [], 512, 48000), 120, [], []);

    public void Store(string path) => _stored[path] = Sample();

    public string Name => "fake";
    public Task<BasicAnalysis?> TryGetAsync(string filePath) => Task.FromResult(_stored.GetValueOrDefault(filePath));
    public Task PutAsync(string filePath, BasicAnalysis analysis) { _stored[filePath] = analysis; return Task.CompletedTask; }
    public Task<IReadOnlyDictionary<string, double>> GetDetectedBpmsAsync() =>
        Task.FromResult<IReadOnlyDictionary<string, double>>(_stored.ToDictionary(p => p.Key, p => p.Value.Bpm));
}
