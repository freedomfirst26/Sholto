namespace Sholto.Storage.Entities;

/// <summary>Links an OUT <see cref="Marker"/> on one deck's track to an IN marker on
/// another track, defining a transition: when playback crosses the out point, deck 2
/// seeks to the in point and starts, applying <see cref="Transition"/> over
/// <see cref="Amount"/> (beats). The trigger engine is a later phase; this models the
/// relationship. From/To are distinct markers (usually on different tracks).</summary>
internal sealed class MarkerLink
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
