using Sholto.App.Analysis.Processing;
using Sholto.App.Analysis.Reporting;
using Sholto.App.Analysis.ToolBoundary;

namespace Sholto.App.ExternalTools;

/// <summary>Builds the Beat This! step over this assembly's internal tool descriptor. Each
/// run's work directory is made under <paramref name="tempRoot"/> (the system temp folder in
/// production).</summary>
public sealed class BeatThisBeatAnalysisStepFactory(
    IToolOutcomeFactory outcomes, IDetectedBeatsFactory detectedBeats, string tempRoot) : IBeatThisBeatAnalysisStepFactory
{
    private readonly IToolOutcomeFactory _outcomes = outcomes;
    private readonly IDetectedBeatsFactory _detectedBeats = detectedBeats;
    private readonly string _tempRoot = tempRoot;

    public IBeatAnalysisStep Create(IExternalTool tool, IAnalysisReporter reporter) =>
        new BeatThisBeatAnalysisStep(tool, reporter, new BeatThisTool(_outcomes, _detectedBeats), _tempRoot);
}
