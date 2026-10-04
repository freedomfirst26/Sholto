using Sholto.App.Analysis.Processing;
using Sholto.App.Analysis.Reporting;
using Sholto.App.Analysis.ToolBoundary;

namespace Sholto.App.ExternalTools;

/// <summary>Builds the analysis steps over this assembly's internal tool descriptors.</summary>
public sealed class MadmomBeatAnalysisStepFactory : IBeatAnalysisStepFactory
{
    public IBeatAnalysisStep Create(IExternalTool tool, IAnalysisReporter reporter) =>
        new MadmomBeatAnalysisStep(tool, reporter, new MadmomTool());
}
