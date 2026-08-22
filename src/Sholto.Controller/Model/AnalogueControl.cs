namespace Sholto.Controller;

/// <summary>A continuous physical control — a fader, knob, or jog. Its state IS
/// its physical position, so <see cref="Reset"/> is a no-op: you never force an
/// analogue control to a value. Startup safety (staying silent until the position
/// is known) comes from soft-takeover in the subclass, not from a reset.</summary>
public abstract class AnalogueControl : Control
{
    protected AnalogueControl(string name) : base(name) { }

    public sealed override void Reset() { }
}
