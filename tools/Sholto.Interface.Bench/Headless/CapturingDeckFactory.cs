using Sholto.App.Audio;
using Sholto.Interface.Bench.Rendering;

namespace Sholto.Interface.Bench.Headless;

/// <summary>
/// Wraps <see cref="BenchDeckFactory"/> and remembers each <see cref="Deck"/> it
/// builds, in creation order. The view model only exposes typed ports, but Bench
/// is a composition root and still needs the concrete Deck for internal members
/// (<c>CurrentFilePath</c>, <c>StemsLoaded</c>) and for <c>AttachEngine</c>.
/// The deck session factory creates Deck1 first, then Deck2, so index 0 is Deck1.
/// </summary>
/// <param name="inner">The factory that actually builds each deck.</param>
public sealed class CapturingDeckFactory(IBenchDeckFactory inner) : IDeckFactory
{
    private readonly List<Deck> _created = [];

    /// <summary>Every deck built so far, in creation order.</summary>
    public IReadOnlyList<Deck> Created => _created;

    public IDeckPorts Create()
    {
        var deck = inner.CreateDeck();
        _created.Add(deck);
        return new DeckPorts(deck);
    }
}
