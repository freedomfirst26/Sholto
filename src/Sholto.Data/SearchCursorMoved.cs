namespace Sholto.Data;

/// <summary>The browse knob turned while search is active; the Interface moves its highlight. Fact: not replayed.</summary>
/// <param name="Delta">Clicks turned; positive is down.</param>
public readonly record struct SearchCursorMoved(int Delta) : IEvent;
