namespace Sholto.App.Analysis.Analyzers;

/// <summary>Lifecycle state of one analysis step on one track.</summary>
public enum AnalysisState
{
    NotStarted,
    Running,
    Complete,
    Failed,
}
