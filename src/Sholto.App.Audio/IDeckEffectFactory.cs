using SoundFlow.Abstracts;
using SoundFlow.Structs;
using SfEngine = SoundFlow.Abstracts.AudioEngine;

namespace Sholto.App.Audio;

/// <summary>The per-deck effect chain.</summary>
public interface IDeckEffectFactory
{
    IReadOnlyList<Func<SfEngine, AudioFormat, SoundModifier>> Chain { get; }
}
