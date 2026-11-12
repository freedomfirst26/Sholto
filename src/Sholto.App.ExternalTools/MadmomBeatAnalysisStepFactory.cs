using Sholto.App.Analysis.Processing;
using Sholto.App.Analysis.Reporting;
using Sholto.App.Analysis.ToolBoundary;

namespace Sholto.App.ExternalTools;

/// <summary>Builds the madmom step over this assembly's internal tool descriptor.</summary>
public sealed class MadmomBeatAnalysisStepFactory(IToolOutcomeFactory outcomes, IDetectedBeatsFactory detectedBeats) : IMadmomBeatAnalysisStepFactory
{
    private readonly IToolOutcomeFactory _outcomes = outcomes;
    private readonly IDetectedBeatsFactory _detectedBeats = detectedBeats;

    public IBeatAnalysisStep Create(IExternalTool tool, IAnalysisReporter reporter) =>
        new MadmomBeatAnalysisStep(tool, reporter, new MadmomTool(_outcomes, _detectedBeats));
}
