using Sholto.Data;

namespace Sholto.Interface.Keyboard;

/// <summary>Recognises each forwarded key press and sends its command at once, on the UI thread, so
/// <c>Handled</c> is set before the window's own key handling continues. A recognised key is always
/// handled and always sent; whether the command runs or is only echoed (the guide is open) is the
/// App's Inspect gate, not this interface's business.</summary>
internal sealed class KeyboardInput(
    IKeyboard keyboard,
    IKeyboardGestureRecognizer recognizer,
    IKeyboardCommandTranslator translator) : IKeyboardInput
{
    private readonly IKeyboard _keyboard = keyboard;
    private readonly IKeyboardGestureRecognizer _recognizer = recognizer;
    private readonly IKeyboardCommandTranslator _translator = translator;

    public void Start() => _keyboard.KeyPressed += e =>
    {
        if (_recognizer.Recognize(new KeyboardEvent(e.Key, e.KeyModifiers)) is not { } gesture) return;
        _translator.Translate(gesture);
        e.Handled = true;
    };
}
