namespace Sholto.Controller;

/// <summary>A lightable button. Input bubbles up via <see cref="Clicked"/> when
/// the physical button is pressed; output is <see cref="SetLit"/>, which the
/// Controller calls to drive the LED. The Button knows nothing about MIDI — it
/// sends light through the <c>applyLight</c> callback the Controller supplies, so
/// all byte-level nuance stays inside the Controller.</summary>
public sealed class Button : Component
{
    private readonly Action<bool> _applyLight;

    public string Name { get; }
    public bool IsLit { get; private set; }

    /// <summary>Fired when the physical button is pressed. Bubbles up to the
    /// Controller, which decides how to light it and what to tell the App.</summary>
    public event Action<Button>? Clicked;

    public Button(string name, Action<bool> applyLight)
    {
        Name = name;
        _applyLight = applyLight;
    }

    /// <summary>Called by the Controller when the matching MIDI press arrives.</summary>
    public void Press() => Clicked?.Invoke(this);

    /// <summary>Set the LED. Idempotent — only touches the hardware on a change.</summary>
    public void SetLit(bool on)
    {
        if (IsLit == on) return;
        IsLit = on;
        _applyLight(on);
    }

    public override void Reset() => SetLit(false);
}
