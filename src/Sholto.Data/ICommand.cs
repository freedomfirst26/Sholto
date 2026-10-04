namespace Sholto.Data;

/// <summary>Marker: "do this". A command is a <c>readonly record struct</c> carrying its strongly
/// typed parameters and an <see cref="Data.Origin"/>. It has no result and is handled by exactly one
/// App handler.
/// <para>Every command exposes where it came from (<see cref="IHasOrigin.Origin"/>) and which deck it
/// targets, so the App can echo, gate or log it generically without boxing the struct. A command
/// with a <c>Deck</c> parameter satisfies <see cref="Deck"/> by that property; a command with no deck
/// declares <c>public int Deck =&gt; -1;</c>.</para></summary>
public interface ICommand : IHasOrigin
{
    /// <summary>The deck (0 or 1) the command targets, or -1 when it is global or the App chooses.</summary>
    int Deck { get; }
}
