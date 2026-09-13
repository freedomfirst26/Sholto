using Sholto.Analysis.Analyzers;
using Sholto.Analysis.Reporting;

namespace Sholto.Analysis.Processing;

/// <summary>
/// Every analysis step name, written once — the single vocabulary
/// <see cref="AnalysisReporter"/>'s required-step set and each step's own
/// <c>StepName</c> draw from.
///
/// Before this existed the same four strings were scattered across four
/// homes in three naming styles (<c>MadmomBeatAnalysisStep.StepNameConst</c>,
/// <c>DemucsStemAnalysisStep.StepNameConst</c>, <c>KeyAnalyzer.KeyStep</c>,
/// <c>BasicAnalysis.WaveformStep</c>), which meant domain code (<see cref="BasicAnalysis"/>)
/// and composition code (<c>App.axaml.cs</c>) had to reach into a specific
/// TOOL ADAPTER just to obtain the string "beats". Follows the
/// <c>ExternalToolNames</c> precedent already established for binary names.
///
/// The string VALUES must never change — they are <see cref="AnalysisReporter"/>'s
/// keys and appear in the UI.
/// </summary>
public static class AnalysisSteps
{
    public const string Waveform = "waveform";
    public const string Beats = "beats";
    public const string Key = "key";
    public const string Stems = "stems";
}
