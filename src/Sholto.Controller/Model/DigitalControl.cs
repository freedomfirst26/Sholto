namespace Sholto.Controller;

/// <summary>A discrete on/off physical control with a light — a button or pad.
/// <see cref="Reset"/> returns its light to off. The Controller supplies the
/// <c>applyLight</c> callback, so the control never touches MIDI itself — all
/// byte-level nuance stays inside the Controller.</summary>
public abstract class DigitalControl : Control
{
    private readonly Action<bool> _applyLight;

    protected DigitalControl(string name, Action<bool> applyLight) : base(name)
        => _applyLight = applyLight;

    public bool IsLit { get; private set; }

    /// <summary>Set the LED. Idempotent — only touches the hardware on a change.</summary>
    public void SetLit(bool on)
    {
        if (IsLit == on) return;
        IsLit = on;
        _applyLight(on);
    }

    /// <summary>Assert the LED off on the hardware, unconditionally. Unlike
    /// <see cref="SetLit"/> this bypasses the idempotency guard: on boot our model
    /// starts <c>IsLit=false</c>, but the physical LED may still be lit from a prior
    /// session or the hardware's own default — so a plain <c>SetLit(false)</c> would
    /// send nothing and leave it glowing. Reset must always emit the note-off.</summary>
    public override void Reset()
    {
        IsLit = false;
        _applyLight(false);
    }
}
