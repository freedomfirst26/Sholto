using SoundFlow.Structs;
using SfEngine = SoundFlow.Abstracts.AudioEngine;

namespace Sholto.App.Audio;

/// <summary>Builds the <see cref="CueOutputRouter"/> that is the playback device's
/// single source.</summary>
public interface ICueOutputRouterFactory
{
    internal CueOutputRouter Create(SfEngine engine, AudioFormat format, IReadOnlyList<IMixSource> decks);

    /// <param name="masterCueActive">Initial MASTER CUE state, re-applied across device switches.</param>
    internal CueOutputRouter Create(SfEngine engine, AudioFormat format, IReadOnlyList<IMixSource> decks, bool masterCueActive);
}
