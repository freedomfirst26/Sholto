namespace Sholto.Interface.Keyboard;

/// <summary>The keyboard as an input interface: key presses forwarded by the window become commands on
/// the data bus. It has no outputs.</summary>
public interface IKeyboardInput
{
    /// <summary>Start listening for key presses.</summary>
    void Start();
}
