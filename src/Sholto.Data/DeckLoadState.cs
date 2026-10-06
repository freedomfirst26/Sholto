namespace Sholto.Data;

/// <summary>Lifecycle states for a deck's currently-loading-or-loaded track. Carried by
/// <see cref="DeckContentChanged"/>.</summary>
public enum DeckLoadState
{
    /// <summary>No track on this deck.</summary>
    Idle,
    /// <summary>Track metadata is showing, but audio samples are still being decoded.</summary>
    Loading,
    /// <summary>Samples decoded and wired up; deck is ready to play.</summary>
    Loaded,
    /// <summary>A previous load attempt failed (decode error, etc.).</summary>
    Failed,
}
