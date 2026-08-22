namespace Sholto.Controller;

/// <summary>A physical control on the controller (button, fader, jog…). The
/// Controller owns a set of these and can reset them all to a known state. Input
/// bubbles UP from a component (e.g. Button.Clicked); the Controller orchestrates
/// output (lighting) back DOWN. The App never sees components — only the
/// high-level semantic events the Controller emits.</summary>
public abstract class Component
{
    /// <summary>Return this control to its default state (e.g. a Button's light
    /// off). Called for every component by <see cref="Controller.Reset"/>.</summary>
    public abstract void Reset();
}
