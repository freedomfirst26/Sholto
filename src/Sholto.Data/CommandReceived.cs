namespace Sholto.Data;

/// <summary>Fact: while Inspect mode is on, a command from a physical input interface was received and
/// deliberately not executed. <paramref name="CommandName"/> is the command type's name, taken from a
/// string built once when the handler was registered, so publishing allocates nothing.
/// <paramref name="Deck"/> is the command's <see cref="ICommand.Deck"/> (-1 for none).</summary>
public readonly record struct CommandReceived(Origin Origin, string CommandName, int Deck) : IEvent;
