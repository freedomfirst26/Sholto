using Avalonia.Input;

namespace Sholto.Interface.Keyboard;

/// <summary>A source of raw key presses — the real <c>MainWindow</c>, or Bench's stand-in. It reports
/// keys; it does not interpret them. <see cref="IKeyboardInput"/> turns each press into a command. Keys
/// with no controller equivalent (search, dialogs, the tag editor, list navigation, ...) are handled
/// inside <c>MainWindow</c> and never raise this event.</summary>
public interface IKeyboard
{
    /// <summary>Raised for each key press that may be a gesture. The handler must set
    /// <c>Handled</c> synchronously if it consumed the key.</summary>
    event Action<KeyEventArgs>? KeyPressed;
}
