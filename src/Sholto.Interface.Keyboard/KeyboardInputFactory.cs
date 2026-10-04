using Sholto.Data;

namespace Sholto.Interface.Keyboard;

public sealed class KeyboardInputFactory : IKeyboardInputFactory
{
    public IKeyboardInput Create(IKeyboard keyboard, ICommandSender sender) =>
        new KeyboardInput(keyboard, new KeyboardGestureRecognizer(), new KeyboardCommandTranslator(sender));
}
