namespace Sholto.Data;

/// <summary>A bus failure: the message type that was being delivered, the handler involved (null when
/// none was registered) and what went wrong.</summary>
public readonly record struct HandlerFailure(Type MessageType, object? Handler, Exception Exception);
