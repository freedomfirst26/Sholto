namespace Sholto.Controller;

/// <summary>A pressable button with no light. Input bubbles up via
/// <see cref="Clicked"/> when the physical button is pressed. A button that also
/// drives an LED is a <see cref="ButtonWithLight"/>.</summary>
public class Button : DigitalControl
{
    /// <summary>Fired when the physical button is pressed. Bubbles up to the
    /// Controller, which decides what to tell the App (and, for a
    /// <see cref="ButtonWithLight"/>, how to light it).</summary>
    public event Action<Button>? Clicked;

    public Button(string name) : base(name) { }

    /// <summary>Called by the Controller when the matching MIDI press arrives.</summary>
    public void Press() => Clicked?.Invoke(this);

    /// <summary>No light to clear — nothing to reset. <see cref="ButtonWithLight"/>
    /// overrides this to switch its LED off.</summary>
    public override void Reset() { }
}
