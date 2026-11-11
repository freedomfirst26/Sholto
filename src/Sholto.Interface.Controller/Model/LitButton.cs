namespace Sholto.Interface.Controller.Model;

/// <summary>A <see cref="Button"/> whose LED shows a <typeparamref name="TLight"/>. The Controller supplies an
/// <c>applyLight</c> callback (which renders the device-specific MIDI), so the control never touches MIDI
/// itself — all byte-level nuance stays in the Controller. <see cref="ButtonWithLight"/> is the on/off
/// case; a pad is a <c>LitButton&lt;PadLight&gt;</c>.</summary>
public class LitButton<TLight>(string name, Action<TLight> applyLight) : Button(name) where TLight : struct
{
    private readonly Action<TLight> _applyLight = applyLight;

    public TLight Light { get; private set; }

    /// <summary>Set the LED. Idempotent — only touches the hardware on a change.</summary>
    public void Set(TLight light)
    {
        if (EqualityComparer<TLight>.Default.Equals(Light, light)) return;
        Light = light;
        _applyLight(light);
    }

    /// <summary>Re-send the current LED state to the hardware unconditionally. Used after a reconnect: the
    /// device comes up dark but our model still holds the intended state, and <see cref="Set"/>'s
    /// idempotency guard would send nothing.</summary>
    public override void Reassert() => _applyLight(Light);

    /// <summary>Assert the LED off on the hardware, unconditionally. Unlike <see cref="Set"/> this bypasses the
    /// idempotency guard: on boot our model starts at <c>default</c>, but the physical LED may still be lit
    /// from a prior session — so a plain <c>Set(default)</c> would send nothing and leave it glowing.</summary>
    public override void Reset()
    {
        Light = default;
        _applyLight(default);
    }
}
