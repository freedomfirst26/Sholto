using Sholto.App.Analysis.Processing;
using Sholto.App.Analysis.Reporting;
using Sholto.App.Analysis.ToolBoundary;

namespace Sholto.App.ExternalTools;

/// <summary>Builds analysis steps whose tool descriptors are internal to
/// <c>Sholto.App.ExternalTools</c>.</summary>
public interface IBeatAnalysisStepFactory
{
    /// <summary>Builds the beat-analysis step that runs through <paramref name="tool"/>; which tool
    /// backs it is the implementation's choice.</summary>
    IBeatAnalysisStep Create(IExternalTool tool, IAnalysisReporter reporter);
}
