namespace Sholto.Data;

/// <summary>Marker: "this thing happened". Published by the App, delivered to every subscriber.
/// Plain events are facts: never replayed to late subscribers. See <see cref="IStateEvent"/>.</summary>
public interface IEvent;
