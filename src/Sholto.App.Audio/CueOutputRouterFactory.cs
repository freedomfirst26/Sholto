using SoundFlow.Structs;
using SfEngine = SoundFlow.Abstracts.AudioEngine;

namespace Sholto.App.Audio;

/// <summary>Default <see cref="ICueOutputRouterFactory"/>.</summary>
public sealed class CueOutputRouterFactory : ICueOutputRouterFactory
{
    CueOutputRouter ICueOutputRouterFactory.Create(SfEngine engine, AudioFormat format, IReadOnlyList<IMixSource> decks) =>
        new(engine, format, decks);

    CueOutputRouter ICueOutputRouterFactory.Create(SfEngine engine, AudioFormat format, IReadOnlyList<IMixSource> decks, bool masterCueActive) =>
        new(engine, format, decks, masterCueActive);
}
