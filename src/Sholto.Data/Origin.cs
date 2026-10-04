namespace Sholto.Data;

/// <summary>Where a command came from: which interface, which control on it, and which gesture
/// (press, hold, turn...) produced it. Convention: every command exposes an <see cref="Origin"/>
/// (implement <see cref="IHasOrigin"/>) so the App can echo, gate or log it by source.</summary>
public readonly record struct Origin(string InterfaceId, string ControlId, string GestureName);
