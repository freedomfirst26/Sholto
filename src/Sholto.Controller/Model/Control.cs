namespace Sholto.Controller;

/// <summary>A control the DJ physically interacts with. Splits into
/// <see cref="AnalogueControl"/> (continuous — faders, knobs, jogs) and
/// <see cref="DigitalControl"/> (discrete — buttons, pads). Every control has a
/// name for logging and identity.</summary>
public abstract class Control : Component
{
    public string Name { get; }

    protected Control(string name) => Name = name;
}
