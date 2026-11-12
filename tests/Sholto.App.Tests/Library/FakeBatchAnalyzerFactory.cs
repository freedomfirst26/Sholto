using Sholto.App.Audio.BatchAnalysis;
using Sholto.App.Library;

namespace Sholto.App.Tests.Library;

/// <summary>Hands out the one analyser it was given (null: no database yet), counting the calls.</summary>
internal sealed class FakeBatchAnalyzerFactory(IBatchAnalyzer? analyzer) : ILibraryBatchAnalyzerFactory
{
    private readonly IBatchAnalyzer? _analyzer = analyzer;

    public int Created { get; private set; }

    public IBatchAnalyzer? Create()
    {
        Created++;
        return _analyzer;
    }
}
