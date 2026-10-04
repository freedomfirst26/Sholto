using Sholto.Data;

namespace Sholto.Interface.Keyboard;

/// <summary>Builds the <see cref="IKeyboardInput"/> over a keyboard.</summary>
public interface IKeyboardInputFactory
{
    IKeyboardInput Create(IKeyboard keyboard, ICommandSender sender);
}
