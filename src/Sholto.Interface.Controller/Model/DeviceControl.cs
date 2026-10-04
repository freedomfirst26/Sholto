namespace Sholto.Interface.Controller;

/// <summary>A control the DJ physically interacts with. Splits into
/// <see cref="AnalogueControl"/> (continuous — faders, knobs, jogs) and
/// <see cref="DigitalControl"/> (discrete — buttons, pads). Every control has a
/// name for logging and identity.</summary>
public abstract class DeviceControl(string name) : Component
{
    public string Name { get; } = name;
}
