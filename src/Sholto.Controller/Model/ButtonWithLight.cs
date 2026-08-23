namespace Sholto.Controller;

/// <summary>A <see cref="Button"/> that also has an LED. The Controller supplies an
/// <c>applyLight</c> callback (which renders the device-specific MIDI), so the
/// control never touches MIDI itself — all byte-level nuance stays in the Controller.</summary>
public sealed class ButtonWithLight : Button
{
    private readonly Action<bool> _applyLight;

    public ButtonWithLight(string name, Action<bool> applyLight) : base(name)
        => _applyLight = applyLight;

    public bool IsLit { get; private set; }

    /// <summary>Set the LED. Idempotent — only touches the hardware on a change.</summary>
    public void SetLit(bool on)
    {
        if (IsLit == on) return;
        IsLit = on;
        _applyLight(on);
    }

    /// <summary>Re-send the current LED state to the hardware unconditionally.
    /// Used after a reconnect: the device comes up dark but our model still holds
    /// the intended state, and <see cref="SetLit"/>'s idempotency guard would send
    /// nothing.</summary>
    public void Reassert() => _applyLight(IsLit);

    /// <summary>Assert the LED off on the hardware, unconditionally. Unlike
    /// <see cref="SetLit"/> this bypasses the idempotency guard: on boot our model
    /// starts <c>IsLit=false</c>, but the physical LED may still be lit from a prior
    /// session — so a plain <c>SetLit(false)</c> would send nothing and leave it
    /// glowing. Reset must always emit the off.</summary>
    public override void Reset()
    {
        IsLit = false;
        _applyLight(false);
    }
}
