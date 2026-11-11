namespace Sholto.Interface.Controller.Model;

/// <summary>A physical control on the controller (button, fader, jog…). The
/// Controller owns a set of these and can reset them all to a known state. Input
/// bubbles UP from a component (e.g. Button.Clicked); the Controller orchestrates
/// output (lighting) back DOWN. The App never sees components — only the
/// high-level semantic events the Controller emits.</summary>
public abstract class Component
{
    /// <summary>Return this control to its default state (e.g. a Button's light
    /// off). Called for every component when the controller is disposed.</summary>
    public abstract void Reset();

    /// <summary>Re-send this control's intended LED state to the hardware, unconditionally (after a reconnect
    /// the device comes up dark). A control with no LED does nothing.</summary>
    public virtual void Reassert() { }
}
