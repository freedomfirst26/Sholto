namespace Sholto.Controller;

/// <summary>A discrete on/off physical control — a button or pad. The pressable
/// behaviour lives in <see cref="Button"/>; a button that also has an LED is a
/// <see cref="ButtonWithLight"/>. This is the discrete counterpart of
/// <see cref="AnalogueControl"/> in the taxonomy; it carries no light itself.</summary>
public abstract class DigitalControl : Control
{
    protected DigitalControl(string name) : base(name) { }
}
