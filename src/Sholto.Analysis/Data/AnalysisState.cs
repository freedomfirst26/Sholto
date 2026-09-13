namespace Sholto.Analysis.Data;

/// <summary>Lifecycle state of one analysis step on one track.</summary>
public enum AnalysisState
{
    NotStarted,
    Running,
    Complete,
    Failed,
}
