namespace Sholto.Interface.Keyboard;

/// <summary>Turns a keyboard gesture into its command and sends it.</summary>
internal interface IKeyboardCommandTranslator
{
    void Translate(in KeyboardGesture gesture);
}
