using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis.Stores;
using Sholto.Data;

namespace Sholto.App.Analysis.Tests;

public class DbAnalysisProviderLogTests
{
    private const string Path = "/music/a.mp3";

    [Fact]
    public async Task A_failed_backfill_is_logged_at_error_with_the_store_name_and_the_exception_chain()
    {
        var computed = new BasicAnalysis(null!, 120, [0.0], [0.0]);
        var logs = new RecordingLogFactory();
        var store = new ThrowingBasicAnalysisStore(
            new InvalidOperationException("outer", new IOException("disk full")));
        var provider = new DbAnalysisProvider(new FixedAnalysisProvider(computed), store, logs);

        var result = await provider.RecomputeAsync(new DecodedTrack(Path, new float[16], 48000, 2));

        Assert.Same(computed, result);
        var line = Assert.Single(logs.Lines);
        Assert.Equal("AnalysisProvider", line.Tag);
        Assert.Equal(LogLevel.Error, line.Level);
        Assert.Equal("backfill to throwing failed: outer  ->  IOException: disk full", line.Message);
    }

    [Fact]
    public async Task A_successful_backfill_logs_nothing()
    {
        var computed = new BasicAnalysis(null!, 120, [0.0], [0.0]);
        var logs = new RecordingLogFactory();
        var provider = new DbAnalysisProvider(
            new FixedAnalysisProvider(computed), new ScriptedBasicAnalysisStore(null), logs);

        await provider.RecomputeAsync(new DecodedTrack(Path, new float[16], 48000, 2));

        Assert.Empty(logs.Lines);
    }

    private sealed class FixedAnalysisProvider(BasicAnalysis analysis) : IAnalysisProvider
    {
        private readonly BasicAnalysis _analysis = analysis;

        public Task<BasicAnalysis> GetAsync(DecodedTrack track, CancellationToken ct = default) =>
            Task.FromResult(_analysis);

        public IBasicAnalysisRequest Begin(string filePath, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<BasicAnalysis> RecomputeAsync(DecodedTrack track, CancellationToken ct = default) =>
            Task.FromResult(_analysis);
    }

    private sealed class ThrowingBasicAnalysisStore(Exception failure) : IBasicAnalysisStore
    {
        private readonly Exception _failure = failure;

        public string Name => "throwing";

        public Task<BasicAnalysis?> TryGetAsync(string filePath) => Task.FromResult<BasicAnalysis?>(null);

        public Task PutAsync(string filePath, BasicAnalysis analysis) => Task.FromException(_failure);

        public Task<IReadOnlyDictionary<string, double>> GetDetectedBpmsAsync() =>
            Task.FromResult<IReadOnlyDictionary<string, double>>(new Dictionary<string, double>());
    }
}
