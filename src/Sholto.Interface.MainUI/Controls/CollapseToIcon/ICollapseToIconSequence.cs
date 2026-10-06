namespace Sholto.Interface.MainUI.Controls.CollapseToIcon;

/// <summary>When a collapse-to-icon consumer is open, collapsing, hinting or idle. The container and the icon
/// wrapper draw from it; it never draws.</summary>
public interface ICollapseToIconSequence
{
    CollapseToIconState State { get; }

    /// <summary>The container is on screen: <see cref="CollapseToIconState.Open"/> or <see cref="CollapseToIconState.Collapsing"/>.</summary>
    bool IsShown { get; }

    /// <summary>Reduced motion: the hint is a still outline rather than pulses.</summary>
    bool IsHintStatic { get; }

    /// <summary>Reduced motion is on: the container fades and never moves.</summary>
    bool Reduced { get; }

    CollapseToIconTimings Timings { get; }

    /// <summary>Raised once per state change.</summary>
    event Action? Changed;

    void Open();

    void Collapse();

    /// <summary>The person found the icon (pointer over it): ends the hint.</summary>
    void TargetEngaged();
}
