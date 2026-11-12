using Sholto.App.Analysis;
using Sholto.App.Analysis.Analyzers.Beats;
using Sholto.App.Analysis.Processing;
using Sholto.App.Analysis.ToolBoundary;

namespace Sholto.App.ExternalTools;

/// <summary>
/// Descriptor for the Beat This! CLI (<c>beat_this</c>, CPJKU), the required beat tracker.
/// It writes its beats to the file named by <c>-o</c>, which is put in the run's own
/// fresh work directory (never beside the track: the music folder may be read-only NTFS
/// and must not be littered with <c>.beats</c> files). Runs on the CPU (<c>--gpu -1</c>)
/// to keep CUDA free for demucs, with the default "minimal" postprocessor — never
/// <c>--dbn</c>, which would pull madmom back in. Postcondition: the file parses to at
/// least one beat.
/// </summary>
internal sealed class BeatThisTool(IToolOutcomeFactory outcomes, IDetectedBeatsFactory detectedBeats) : IToolDefinition<DetectedBeats>
{
    /// <summary>The file, inside <see cref="ToolInput.WorkDir"/>, that the run writes its beats to.</summary>
    public const string OutputFileName = "track.beats";

    private readonly IToolOutcomeFactory _outcomes = outcomes;
    private readonly IDetectedBeatsFactory _detectedBeats = detectedBeats;

    public string Name => AnalysisSteps.Beats;
    public string BinaryName => ExternalToolNames.BeatThis;
    public bool IsRequired => true;

    public IReadOnlyList<string> BuildArgs(ToolInput toolInput) =>
        new[] { toolInput.Input, "-o", OutputPath(toolInput), "--gpu", "-1" };

    public ToolOutcome<DetectedBeats> Verify(ToolInput toolInput, string stdout, int exitCode)
    {
        if (exitCode != 0)
            return _outcomes.Failure<DetectedBeats>($"beat_this exited with code {exitCode}");

        var output = OutputPath(toolInput);
        if (!File.Exists(output))
            return _outcomes.Failure<DetectedBeats>("beat_this exited 0 but wrote no beats file");

        var beats = _detectedBeats.FromBeatLines(File.ReadAllText(output));
        if (beats.BeatTimes.Length == 0)
            return _outcomes.Failure<DetectedBeats>("beat_this exited 0 but produced no beats");

        return _outcomes.Success(beats);
    }

    private string OutputPath(ToolInput toolInput) => Path.Combine(toolInput.WorkDir, OutputFileName);
}
