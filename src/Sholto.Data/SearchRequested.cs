namespace Sholto.Data;

/// <summary>Something asked for the search overlay (the browse push). Fact: not replayed. The Interface opens the
/// overlay if closed, or toggles table/rail focus if open.</summary>
/// <param name="Origin">Who asked.</param>
public readonly record struct SearchRequested(Origin Origin) : IEvent;
