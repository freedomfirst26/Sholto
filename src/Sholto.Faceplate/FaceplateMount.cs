namespace Sholto.Faceplate;

/// <summary>Where the controller guide is shown and how big — deliberately separate
/// from <see cref="Sholto.Controller.Gestures.GestureRouting"/>, which answers whether
/// the decks respond. Only <see cref="Full"/> is built today; the others are wired so
/// the seam exists.</summary>
public enum FaceplateMount
{
    /// <summary>Not shown.</summary>
    Hidden,
    /// <summary>Takes most of the screen. The full guide: labels, the gesture table,
    /// layers and combinations. The only mount built today.</summary>
    Full,
    /// <summary>A column beside the track list, as a live mirror of your hands while you
    /// work — no labels, no panel. NOT BUILT YET. Selecting it currently behaves as
    /// <see cref="Full"/>.</summary>
    Embedded,
}
