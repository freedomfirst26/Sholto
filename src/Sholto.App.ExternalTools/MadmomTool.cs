using Sholto.App.Analysis;
using Sholto.App.Analysis.Analyzers.Beats;
using Sholto.App.Analysis.Processing;
using Sholto.App.Analysis.ToolBoundary;

namespace Sholto.App.ExternalTools;

/// <summary>Descriptor for madmom's DBNDownBeatTracker, the optional fallback beat tracker
/// used only when Beat This! is not installed. Postcondition: stdout parses to at least
/// one beat.</summary>
internal sealed class MadmomTool(IToolOutcomeFactory outcomes, IDetectedBeatsFactory detectedBeats) : IToolDefinition<DetectedBeats>
{
    private readonly IToolOutcomeFactory _outcomes = outcomes;
    private readonly IDetectedBeatsFactory _detectedBeats = detectedBeats;

    public string Name => AnalysisSteps.Beats;
    public string BinaryName => ExternalToolNames.Madmom;

    /// <summary>Analysis cannot proceed without it when it is the beat tracker in use.</summary>
    public bool IsRequired => true;

    public IReadOnlyList<string> BuildArgs(ToolInput toolInput) =>
        new[] { "--beats_per_bar", "3,4", "single", toolInput.Input };

    public ToolOutcome<DetectedBeats> Verify(
        ToolInput toolInput, string stdout, int exitCode)
    {
        if (exitCode != 0)
            return _outcomes.Failure<DetectedBeats>(
                $"madmom exited with code {exitCode}");

        var beats = _detectedBeats.FromBeatLines(stdout);
        if (beats.BeatTimes.Length == 0)
            return _outcomes.Failure<DetectedBeats>(
                "madmom exited 0 but produced no beats");

        return _outcomes.Success(beats);
    }
}
