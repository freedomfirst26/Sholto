namespace Sholto.Data;

/// <summary>Stop the running library analysis: no further songs start. Ignored when none is running.</summary>
public readonly record struct CancelLibraryAnalysis(Origin Origin) : ICommand;
