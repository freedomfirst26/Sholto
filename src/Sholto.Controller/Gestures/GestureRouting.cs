namespace Sholto.Controller.Gestures;

/// <summary>Whether gestures still reach the decks while the guide is shown. Deliberately
/// separate from <see cref="Sholto.Faceplate.FaceplateMount"/>: where the guide sits and
/// whether the decks respond are independent questions.</summary>
public enum GestureRouting
{
    /// <summary>The decks respond as normal.</summary>
    Play,
    /// <summary>The decks are suspended. Press PLAY to find out what PLAY does without
    /// starting it. Achieved by disabling the App's GestureBindings, not by discarding
    /// gestures — the guide still sees everything.</summary>
    Inspect,
}
