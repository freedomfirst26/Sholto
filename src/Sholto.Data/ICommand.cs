namespace Sholto.Data;

/// <summary>Marker: "do this". A command is a <c>readonly record struct</c> carrying its strongly
/// typed parameters and an <see cref="Data.Origin"/>. It has no result and is handled by exactly one
/// App handler.
/// <para>A command that acts on a deck has an <c>int Deck</c> parameter of its own; the deck side of the
/// control that issued it travels on <see cref="Origin.Deck"/>.</para></summary>
public interface ICommand : IHasOrigin;
