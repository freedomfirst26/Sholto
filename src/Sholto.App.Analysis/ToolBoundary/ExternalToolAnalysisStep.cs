using Sholto.App.Analysis.Processing;

namespace Sholto.App.Analysis.ToolBoundary;

/// <summary>
/// Base for every analysis step performed by one arm's-length external tool through
/// an <see cref="IExternalTool"/>. Supplies <see cref="IsAvailable"/>
/// as a pure forward onto the configured tool — the one collaborator each step's
/// constructor now takes — leaving <see cref="StepName"/> and the actual
/// <c>AnalyzeAsync</c> to each concrete adapter.
/// </summary>
public abstract class ExternalToolAnalysisStep(IExternalTool tool) : IAnalysisStep
{
    protected IExternalTool Tool { get; } = tool;

    public abstract string StepName { get; }

    public bool IsAvailable => Tool.IsAvailable;
}
