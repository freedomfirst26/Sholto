using Sholto.Analysis;
using Sholto.Analysis.Processing;

namespace Sholto.Bench.Rendering;

/// <summary>
/// No-op collaborators for building a real <see cref="Sholto.Audio.Deck"/> in
/// Bench. Deck's own decode/stem ports run only off <c>Deck.Load</c>
/// (full in-memory load); Bench always uses <c>Deck.LoadStreaming</c>, whose
/// background analysis kick-off calls <see cref="NoOpAudioFileDecoder.Decode"/>
/// first, which always throws — caught and logged inside Deck's own
/// background task, never surfaced further. They exist only so Deck's
/// constructor is satisfiable; a scenario that needs real decode/stem
/// behaviour is out of scope for this render harness.
/// </summary>
internal sealed class NoOpAudioFileDecoder : Sholto.Audio.IAudioFileDecoder
{
    public static readonly NoOpAudioFileDecoder Instance = new();
    private NoOpAudioFileDecoder() { }
    public float[] Decode(string filePath) =>
        throw new NotSupportedException("Sholto.Bench renders via Deck.LoadStreaming, which never decodes through this port.");
}

/// <summary>Implements <see cref="IStemAnalysisStep"/>, needed by <c>Deck</c>'s
/// constructor. <c>MainViewModel</c>'s stem-presence role is a separate concrete
/// <see cref="DemucsStemPresence"/> instance now (see <c>CachingStemAnalysisStep</c>
/// in <c>Sholto.App/App.axaml.cs</c> for why the two roles are split); this no-op
/// only covers the analysis-step role, since it's never actually called here.</summary>
internal sealed class NoOpStemAnalyzer : IStemAnalysisStep
{
    public static readonly NoOpStemAnalyzer Instance = new();
    private NoOpStemAnalyzer() { }
    public string StepName => AnalysisSteps.Stems;
    public bool IsAvailable => false;
    public Task<StemPaths> AnalyzeAsync(string filePath, IAnalysisReporter reporter, CancellationToken ct = default) =>
        throw new NotSupportedException("Sholto.Bench renders via Deck.LoadStreaming, which never calls the stem analyzer.");
}
