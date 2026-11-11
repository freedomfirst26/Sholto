using Microsoft.Extensions.Options;
using SoundFlow.Structs;
using SfEngine = SoundFlow.Abstracts.AudioEngine;

namespace Sholto.App.Audio;

/// <summary>Default <see cref="ICueOutputRouterFactory"/>. Reads the callback-timing switch once.</summary>
public sealed class CueOutputRouterFactory(IOptions<AudioTimingOptions> timing) : ICueOutputRouterFactory
{
    private readonly bool _timingEnabled = timing.Value.Enabled;

    CueOutputRouter ICueOutputRouterFactory.Create(SfEngine engine, AudioFormat format, IReadOnlyList<IMixSource> decks) =>
        new(engine, format, decks, _timingEnabled);

    CueOutputRouter ICueOutputRouterFactory.Create(SfEngine engine, AudioFormat format, IReadOnlyList<IMixSource> decks, bool masterCueActive) =>
        new(engine, format, decks, _timingEnabled, masterCueActive);
}
