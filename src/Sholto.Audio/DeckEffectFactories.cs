using SoundFlow.Abstracts;
using SoundFlow.Structs;
using SfEngine = SoundFlow.Abstracts.AudioEngine;

namespace Sholto.Audio;

/// <summary>
/// One deck's post-mix effect chain, as an ORDERED list of factories rather
/// than hardcoded types. <see cref="Deck.AttachEngine"/> walks this in order
/// and adds each built modifier to the deck's mixer — order is the chain
/// order (EQ → filter → echo today), because SoundFlow's own
/// <c>SoundComponent.Process</c> iterates its modifier array in the order
/// modifiers were added.
///
/// A factory can't run before <c>AttachEngine</c> has the SoundFlow engine
/// and format — that constraint is why this is a list of delegates handed in
/// at composition (<see cref="DeckFactory"/>/<c>App.axaml.cs</c>,
/// <c>BenchDeckFactory</c>) rather than a list of already-built modifiers.
///
/// Adding a fourth effect is one factory added to <see cref="Default"/> —
/// no change to <see cref="Deck"/>, <see cref="IDeckMixer"/>, or
/// <see cref="DeckMixer"/> (whose <c>AttachModifiers</c> looks up the 3
/// effects it needs by type, ignoring anything else in the list).
/// </summary>
public static class DeckEffectFactories
{
    /// <summary>Today's chain, in today's order — EQ, then the COLOR filter
    /// (post-EQ, matching the Pioneer channel-strip order), then the echo
    /// (post-filter). Shared by the real app (<c>App.axaml.cs</c>) and
    /// Sholto.Bench (<c>BenchDeckFactory</c>) so both build the identical
    /// chain.</summary>
    public static readonly IReadOnlyList<Func<SfEngine, AudioFormat, SoundModifier>> Default =
    [
        (engine, format) => new BiquadEq3Band(engine, format),
        (engine, format) => new DjFilter(engine, format),
        (engine, format) => new EchoEffect(engine, format),
        (engine, format) => new BeatRepeatEffect(engine, format),
    ];
}
