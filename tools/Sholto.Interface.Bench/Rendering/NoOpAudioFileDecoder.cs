using Sholto.App.Analysis;
using Sholto.App.Analysis.Processing;
using Sholto.App.Analysis.Reporting;
using Sholto.App.Analysis.Stems;

namespace Sholto.Interface.Bench.Rendering;

/// <summary>
/// No-op collaborators for building a real <see cref="Sholto.App.Audio.Deck"/> in
/// Bench. Deck's own decode/stem ports run only off <c>Deck.Load</c>
/// (full in-memory load); Bench always uses <c>Deck.LoadStreaming</c>, whose
/// background analysis kick-off calls <see cref="NoOpAudioFileDecoder.Decode"/>
/// first, which always throws — caught and logged inside Deck's own
/// background task, never surfaced further. They exist only so Deck's
/// constructor is satisfiable; a scenario that needs real decode/stem
/// behaviour is out of scope for this render harness.
/// </summary>
public sealed class NoOpAudioFileDecoder : Sholto.App.Audio.IAudioFileDecoder
{
    public float[] Decode(string filePath) =>
        throw new NotSupportedException("Sholto.Interface.Bench renders via Deck.LoadStreaming, which never decodes through this port.");
}
