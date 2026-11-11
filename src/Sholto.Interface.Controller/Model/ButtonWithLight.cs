namespace Sholto.Interface.Controller.Model;

/// <summary>A <see cref="Button"/> that also has an on/off LED: <see cref="LitButton{TLight}"/> of
/// <c>bool</c>, with the names the callers use.</summary>
public sealed class ButtonWithLight(string name, Action<bool> applyLight) : LitButton<bool>(name, applyLight)
{
    public bool IsLit => Light;

    /// <summary>Set the LED. Idempotent — only touches the hardware on a change.</summary>
    public void SetLit(bool on) => Set(on);
}
