using Microsoft.Extensions.Options;
using Sholto.App.Audio.Recording;
using SoundFlow.Structs;
using SfEngine = SoundFlow.Abstracts.AudioEngine;

namespace Sholto.App.Audio;

/// <summary>Default <see cref="ICueOutputRouterFactory"/>. Reads the callback-timing switch once. The two overloads
/// without a tap (the offline renders) give the router a <see cref="NullMasterTap"/>.</summary>
public sealed class CueOutputRouterFactory(IOptions<AudioTimingOptions> timing) : ICueOutputRouterFactory
{
    private readonly bool _timingEnabled = timing.Value.Enabled;
    private readonly IMasterTap _noTap = new NullMasterTap();

    CueOutputRouter ICueOutputRouterFactory.Create(SfEngine engine, AudioFormat format, IReadOnlyList<IMixSource> decks) =>
        new(engine, format, decks, _timingEnabled, _noTap);

    CueOutputRouter ICueOutputRouterFactory.Create(SfEngine engine, AudioFormat format, IReadOnlyList<IMixSource> decks, bool masterCueActive) =>
        new(engine, format, decks, _timingEnabled, _noTap, masterCueActive);

    CueOutputRouter ICueOutputRouterFactory.Create(SfEngine engine, AudioFormat format, IReadOnlyList<IMixSource> decks, bool masterCueActive,
        IMasterTap masterTap) =>
        new(engine, format, decks, _timingEnabled, masterTap, masterCueActive);
}
