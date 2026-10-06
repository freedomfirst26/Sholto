namespace Sholto.Data;

/// <summary>A load passed the confirm guard and started. Fact: not replayed. Not raised for an undo restore.</summary>
/// <param name="Deck">The deck loaded.</param>
/// <param name="FilePath">The track's file path.</param>
/// <param name="Title">The track's title.</param>
/// <param name="Artist">The track's artist.</param>
public readonly record struct LoadAccepted(int Deck, string FilePath, string Title, string Artist) : IEvent;
