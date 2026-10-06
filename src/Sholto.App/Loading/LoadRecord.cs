using Sholto.App.Library;

namespace Sholto.App.Loading;

/// <summary>What the last accepted load displaced, and what it put there: enough to put the deck back.</summary>
/// <param name="Deck">The deck the load targeted.</param>
/// <param name="Previous">The track that was on the deck, or null if it was empty.</param>
/// <param name="PreviousPosition">Where that track's playhead was, as a fraction of its length.</param>
/// <param name="Loaded">The track the load put on the deck.</param>
/// <param name="At">When the load was accepted.</param>
public readonly record struct LoadRecord(int Deck, Track? Previous, double PreviousPosition, Track Loaded, DateTime At);
