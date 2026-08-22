namespace Sholto.Storage.Entities;

/// <summary>How the deck-to-deck transition plays out when a link fires.</summary>
public enum TransitionType
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

/// <summary>Links an OUT <see cref="Marker"/> on one deck's track to an IN marker on
/// another track, defining a transition: when playback crosses the out point, deck 2
/// seeks to the in point and starts, applying <see cref="Transition"/> over
/// <see cref="Amount"/> (beats). The trigger engine is a later phase; this models the
/// relationship. From/To are distinct markers (usually on different tracks).</summary>
public sealed class MarkerLink
{
    public int Id { get; set; }
    /// <summary>The OUT marker whose crossing fires the transition.</summary>
    public int FromMarkerId { get; set; }
    /// <summary>The IN marker the other deck seeks to and starts from.</summary>
    public int ToMarkerId { get; set; }
    public TransitionType Transition { get; set; } = TransitionType.Crossfade;
    /// <summary>Transition length in beats (interpretation depends on Transition).</summary>
    public double Amount { get; set; } = 16;
    public DateTime CreatedAt { get; set; }

    public Marker FromMarker { get; set; } = null!;
    public Marker ToMarker { get; set; } = null!;
}
