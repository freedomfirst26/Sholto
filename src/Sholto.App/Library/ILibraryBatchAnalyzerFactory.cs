using Sholto.App.Audio.BatchAnalysis;

namespace Sholto.App.Library;

/// <summary>Builds the batch analyser a library analysis runs on.</summary>
public interface ILibraryBatchAnalyzerFactory
{
    /// <summary>A batch analyser over the decks' analysis chain and the library's stores, or null while
    /// there is nowhere to keep results (no library database yet).</summary>
    IBatchAnalyzer? Create();
}
