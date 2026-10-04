namespace Sholto.Interface.Keyboard;

/// <summary>Turns a raw <see cref="KeyboardEvent"/> into the gesture it means, or null for a key that is
/// not a gesture.</summary>
internal interface IKeyboardGestureRecognizer
{
    KeyboardGesture? Recognize(KeyboardEvent key);
}
