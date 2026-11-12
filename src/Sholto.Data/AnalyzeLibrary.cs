namespace Sholto.Data;

/// <summary>Analyse beats and key for every song in the library catalog that is not analysed yet, a few at
/// a time in the background (the Settings menu entry). Ignored while a library analysis is already running.
/// The App answers with <see cref="LibraryAnalysisProgressChanged"/>.</summary>
public readonly record struct AnalyzeLibrary(Origin Origin) : ICommand;
