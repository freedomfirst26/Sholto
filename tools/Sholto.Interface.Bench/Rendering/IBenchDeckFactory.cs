using Sholto.App.Audio;
using SoundFlow.Structs;

namespace Sholto.Interface.Bench.Rendering;

/// <summary>
/// What <see cref="BenchDeck"/>, <see cref="DeckAdvance"/> and
/// <see cref="Sholto.Interface.Bench.Headless.CapturingDeckFactory"/> take from Bench's deck factory: the
/// <see cref="IDeckFactory"/> port plus the concrete <see cref="Deck"/> (Bench is a
/// composition root and needs internals and <c>AttachEngine</c>) and the format
/// every Bench deck is attached with.
/// </summary>
public interface IBenchDeckFactory : IDeckFactory
{
    /// <summary>The format every Bench deck is attached with.</summary>
    AudioFormat DeckFormat { get; }

    /// <summary>The concrete Deck, not wrapped in its ports.</summary>
    Deck CreateDeck();
}
