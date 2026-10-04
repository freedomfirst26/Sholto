namespace Sholto.Storage.Entities;

/// <summary>How the deck-to-deck transition plays out when a link fires.</summary>
internal enum TransitionType
{
    /// <summary>Hard switch — cut from the out point straight to the in point.</summary>
    Cut,
    /// <summary>Crossfade over <see cref="MarkerLink.Amount"/> beats.</summary>
    Crossfade,
    /// <summary>Echo/delay tail out of deck 1 while deck 2 comes in.</summary>
    Echo,
    /// <summary>Filter sweep out of deck 1 into deck 2.</summary>
    Filter,
}
