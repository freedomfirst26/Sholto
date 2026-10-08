namespace Sholto.Data;

/// <summary>Fact: while Inspect mode is on, a command from a physical input interface was received and
/// deliberately not executed. <paramref name="CommandName"/> is the command type's name, taken from a
/// string built once when the handler was registered, so publishing allocates nothing. The deck side of
/// the control that issued it is <see cref="Origin.Deck"/> on <paramref name="Origin"/>.</summary>
public readonly record struct CommandReceived(Origin Origin, string CommandName) : IEvent;
