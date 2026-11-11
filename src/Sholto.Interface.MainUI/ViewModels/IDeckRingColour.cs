namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>The disc's outer ring colour: green (0 %) -> yellow (50 %) -> orange (75 %) -> red (100 %) along
/// the track, from the active theme's ring palette.</summary>
public interface IDeckRingColour
{
    /// <summary>One cached brush whose Color is mutated in place, and only when it changes: no allocation per
    /// frame. The same instance for life.</summary>
    Avalonia.Media.IBrush Brush { get; }

    /// <summary>Recolours the brush for the playhead (0..1); assigns only on a real change.</summary>
    void Recompute(double playPosition);

    /// <summary>Recolours for the playhead and returns true when the visible colour has moved enough to
    /// notify (every ~1 % of track length, ~2 s of music).</summary>
    bool MoveTo(double playPosition);
}
