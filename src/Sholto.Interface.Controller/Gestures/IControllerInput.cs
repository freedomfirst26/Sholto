namespace Sholto.Interface.Controller;

/// <summary>The controller as an input interface: turns what the DJ does on the hardware into commands
/// on the data bus. Gestures are private to it.</summary>
public interface IControllerInput
{
    /// <summary>Start listening to the control surface and ticking the browse-hold timer.</summary>
    void Start();
}
