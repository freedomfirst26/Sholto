using Microsoft.Extensions.Options;
using Sholto.App.Analysis.Analyzers.Keys;
using Sholto.App.Analysis.Stores;
using Sholto.App.Audio;
using Sholto.App.Audio.BatchAnalysis;
using Sholto.App.Decks;

namespace Sholto.App.Library;

/// <summary>Builds a <see cref="BatchAnalyzer"/> over the same analysis chain a deck load uses (deck 1's
/// provider, as re-analysis does) and the same key store, so whatever a library analysis stores a later
/// deck load reads as a cache hit. The basic-analysis store is the one the library scanned with.</summary>
public sealed class LibraryBatchAnalyzerFactory(
    IAudioFileDecoder decoder,
    IKeyAnalysisStore keyStore,
    IKeyAnalyzer keyAnalyzer,
    IDecks decks,
    ILibrarySession library,
    IOptions<BatchAnalysisOptions> options) : ILibraryBatchAnalyzerFactory
{
    private readonly IAudioFileDecoder _decoder = decoder;
    private readonly IKeyAnalysisStore _keyStore = keyStore;
    private readonly IKeyAnalyzer _keyAnalyzer = keyAnalyzer;
    private readonly IDecks _decks = decks;
    private readonly ILibrarySession _library = library;
    private readonly IOptions<BatchAnalysisOptions> _options = options;

    public IBatchAnalyzer? Create()
    {
        var provider = _decks.Deck1.Loading.AnalysisProvider;
        var basicStore = _library.BasicAnalyses;
        if (provider is null || basicStore is null) return null;
        return new BatchAnalyzer(_decoder, provider, basicStore, _keyStore, _keyAnalyzer, _options);
    }
}
