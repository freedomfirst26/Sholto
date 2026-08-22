namespace Sholto.Controller;

/// <summary>A pressable, lightable button. Input bubbles up via
/// <see cref="Clicked"/> when the physical button is pressed; output (the LED) is
/// <c>SetLit</c>, inherited from <see cref="DigitalControl"/>, which the
/// Controller calls to orchestrate lighting.</summary>
public sealed class Button : DigitalControl
{
    /// <summary>Fired when the physical button is pressed. Bubbles up to the
    /// Controller, which decides how to light it and what to tell the App.</summary>
    public event Action<Button>? Clicked;

    public Button(string name, Action<bool> applyLight) : base(name, applyLight) { }

    /// <summary>Called by the Controller when the matching MIDI press arrives.</summary>
    public void Press() => Clicked?.Invoke(this);
}
