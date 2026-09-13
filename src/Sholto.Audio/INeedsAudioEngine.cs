using SfEngine = SoundFlow.Abstracts.AudioEngine;

namespace Sholto.Audio;

/// <summary>
/// Something that cannot work until it has been handed the SoundFlow engine, which
/// only exists once <see cref="AudioEngine"/> has constructed it.
///
/// Why this exists: <see cref="AudioEngine"/> used to take
/// <see cref="FlacDecodeStrategy"/> by its concrete type, purely to call
/// <c>AttachEngine</c> on it — which meant the engine knew that one specific audio
/// format existed and needed special handling. It doesn't care about FLAC; it cares
/// that some collaborators need the engine after construction, exactly as each
/// <see cref="Deck"/> does. A future strategy with the same need implements this and
/// is passed in at the composition root; <see cref="AudioEngine"/> never changes.
///
/// The order dependence is real and deliberate: anything implementing this throws a
/// clear error if used before attachment, rather than silently returning nothing.
/// </summary>
public interface INeedsAudioEngine
{
    /// <summary>Hand over the engine. Called once, by <see cref="AudioEngine"/>'s
    /// constructor, before any decoding is attempted.</summary>
    void AttachEngine(SfEngine engine);
}
