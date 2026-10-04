using Avalonia.Input;

namespace Sholto.Interface.Keyboard.Tests;

/// <summary>A keyboard the test presses keys on; returns the event args so <c>Handled</c> can be read.</summary>
internal sealed class FakeKeyboard : IKeyboard
{
    public event Action<KeyEventArgs>? KeyPressed;

    public KeyEventArgs Press(Key key, KeyModifiers modifiers = KeyModifiers.None)
    {
        var args = new KeyEventArgs { Key = key, KeyModifiers = modifiers };
        KeyPressed?.Invoke(args);
        return args;
    }
}
